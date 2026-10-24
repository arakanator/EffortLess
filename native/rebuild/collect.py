"""Fail-closed DLL/source collector. Run with native UCRT64 Python inside MSYS2."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tarfile
import tempfile
import tomllib


def run(*args):
    result = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if result.returncode:
        raise RuntimeError(f"Command failed: {args[0]} {args[1:]}\n{result.stderr[-3000:]}")
    return result.stdout


def native(path):
    return Path(run("cygpath", "-w", str(path)).strip())


def posix(path):
    return run("cygpath", "-u", str(path)).strip()


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def fields(text):
    result = {}
    for line in text.splitlines():
        if " = " in line and not line.startswith("#"):
            key, value = line.split(" = ", 1)
            result.setdefault(key, []).append(value)
    return result


SYSTEM = {n + ".dll" for n in (
    "advapi32 avrt bcrypt bcryptprimitives cfgmgr32 combase comctl32 comdlg32 crypt32 cryptbase "
    "d3d9 d3d10 d3d10_1 d3d11 d3d12 d3dcompiler_47 dcomp dinput8 dnsapi dsound dwmapi dwrite "
    "dxgi dxva2 gdi32 gdi32full glu32 hid imm32 iphlpapi kernel32 kernelbase mf mfplat "
    "mfreadwrite mpr msacm32 msasn1 msvcrt msvfw32 ncrypt netapi32 normaliz ntdll ole32 "
    "oleaut32 opengl32 powrprof propsys psapi rpcrt4 secur32 setupapi shell32 shlwapi "
    "ucrtbase user32 userenv uxtheme version win32u winhttp winmm wintrust wintypes "
    "wldap32 ws2_32 wtsapi32"
).split()}


def package_archive(name, version, cache):
    for candidate in cache.glob(name + "-*.pkg.tar.*"):
        if candidate.name.endswith(".sig"):
            continue
        text = run("bsdtar", "-xOf", str(candidate), ".PKGINFO")
        data = fields(text)
        if data.get("pkgname") == [name] and data.get("pkgver") == [version]:
            return candidate, data, text
    raise RuntimeError("Exact binary package absent from cache: " + name + " " + version)


def cargo_sources(source, entries, output):
    """Retain hash-locked registry crates and their notices, including static deps."""
    records = []
    locks_found = 0
    for entry in entries:
        if not entry.endswith((".tar.gz", ".tar.xz", ".tar.bz2", ".tgz", ".tar")):
            continue
        with tempfile.TemporaryDirectory() as scratch:
            nested = Path(scratch) / "upstream.tar"
            with nested.open("wb") as stream:
                subprocess.run(["bsdtar", "-xOf", str(source), entry], stdout=stream, check=True)
            with tarfile.open(nested) as archive:
                locks = [m for m in archive if m.isfile() and Path(m.name).name == "Cargo.lock"]
                for lock in locks:
                    locks_found += 1
                    data = tomllib.loads(archive.extractfile(lock).read().decode())
                    for crate in data.get("package", []):
                        origin = crate.get("source")
                        if not origin:
                            continue
                        if origin != "registry+https://github.com/rust-lang/crates.io-index":
                            raise RuntimeError("Non-registry Rust dependency needs explicit source archival: " + origin)
                        name, version = crate["name"], crate["version"]
                        if not re.fullmatch(r"[A-Za-z0-9_-]+", name) or not re.fullmatch(r"[A-Za-z0-9.+_-]+", version):
                            raise RuntimeError("Unsafe Cargo dependency name/version")
                        target = output / "sources" / "cargo" / (name + "-" + version + ".crate")
                        target.parent.mkdir(parents=True, exist_ok=True)
                        if not target.exists():
                            run("curl", "--fail", "--location", "--retry", "2", "--proto", "=https",
                                "--proto-redir", "=https", "--output", str(target),
                                f"https://static.crates.io/crates/{name}/{name}-{version}.crate")
                        if sha(target) != crate.get("checksum"):
                            raise RuntimeError("Cargo source checksum mismatch: " + target.name)
                        with tarfile.open(target) as crate_archive:
                            for member in crate_archive:
                                leaf = Path(member.name).name
                                if member.isfile() and re.match(r"(?i)^(license|licence|copying|copyright|notice)", leaf):
                                    dest = output / "licenses" / "cargo" / (name + "-" + version) / leaf
                                    dest.parent.mkdir(parents=True, exist_ok=True)
                                    dest.write_bytes(crate_archive.extractfile(member).read())
                        records.append({"crate": name, "version": version, "sha256": sha(target)})
    if not locks_found:
        raise RuntimeError("Rust native package lacks a retained Cargo.lock source closure")
    return records


def collect_package(name, output):
    version = run("pacman", "-Q", name).strip().split()[1]
    archive, info, text = package_archive(name, version, native("/var/cache/pacman/pkg"))
    if "ffmpeg" in name:
        raise RuntimeError("Prebuilt GPL FFmpeg must not enter LGPL payload: " + name)
    base = info["pkgbase"][0]
    buildinfo = run("bsdtar", "-xOf", str(archive), ".BUILDINFO")
    build = fields(buildinfo)
    directory = output / "sources" / base
    directory.mkdir(parents=True, exist_ok=True)
    # Epoch is part of the installed version, but not the archive filename.
    filename = base + "-" + version.split(":")[-1] + ".src.tar.zst"
    source = directory / filename
    url = "https://mirror.msys2.org/mingw/sources/" + filename
    for suffix in ("", ".sig"):
        run("curl", "--fail", "--location", "--retry", "2", "--proto", "=https",
            "--proto-redir", "=https", "--output", str(source) + suffix, url + suffix)
    run("bash", "/usr/bin/pacman-key", "--verify", posix(str(source) + ".sig"), posix(source))
    entries = run("bsdtar", "-tf", str(source)).splitlines()
    recipe = next((e for e in entries if Path(e).name == "PKGBUILD"), None)
    srcinfo = next((e for e in entries if Path(e).name == ".SRCINFO"), None)
    if not recipe or not srcinfo:
        raise RuntimeError("Source archive lacks recipe/metadata: " + filename)
    recipe_bytes = subprocess.check_output(["bsdtar", "-xOf", str(source), recipe])
    expected_recipe = build.get("pkgbuild_sha256sum", [])
    if len(expected_recipe) != 1 or hashlib.sha256(recipe_bytes).hexdigest() != expected_recipe[0]:
        raise RuntimeError("Source recipe does not match binary BUILDINFO: " + filename)
    upstream = [e for e in entries if e.endswith((".tar.gz", ".tar.xz", ".tar.bz2", ".tar.zst", ".tgz", ".zip", ".tar"))]
    # Do not count patches or a recipe-only download as corresponding source.
    if not upstream and not any("/objects/" in e for e in entries):
        raise RuntimeError("Full upstream source absent: " + filename)
    licenses = []
    for row in run("pacman", "-Ql", name).splitlines():
        _, _, member = row.partition(" ")
        if "/share/licenses/" not in member:
            continue
        path = native(member)
        if path.is_file():
            destination = output / "licenses" / name / member.split("/share/licenses/", 1)[1]
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, destination)
            licenses.append(destination)
    if not licenses:
        raise RuntimeError("No copyright/license files for runtime package: " + name)
    labels = info.get("license", [])
    evidence = "\n".join(p.read_text(encoding="utf-8", errors="replace") for p in licenses)
    gcc_exception = "gcc" in name and "GCC RUNTIME LIBRARY EXCEPTION" in evidence.upper()
    for label in labels:
        upper = label.upper()
        # OR permits selecting a compatible branch; AND with GPL-only does not.
        if re.search(r"(?<!L)GPL", upper) and not gcc_exception:
            if not (" OR " in upper and " AND " not in upper and re.search(r"MIT|BSD|APACHE|ISC|LGPL", upper)):
                raise RuntimeError("GPL-only package needs component-level assessment: " + name + " " + label)
        if not re.search(r"LGPL|GPL|MIT|BSD|APACHE|ISC|ZLIB|FTL|MPL|X11|UNICODE|CC0|BSL|UNLICENSE|PUBLIC", upper):
            if not (upper == "CUSTOM" and "Permission is hereby granted" in evidence):
                raise RuntimeError("Unsupported license evidence: " + name + " " + label)
    if not labels:
        raise RuntimeError("Missing license metadata: " + name)
    crates = cargo_sources(source, upstream, output) if b"cargo" in recipe_bytes else []
    for member, content in (("PKGINFO", text), ("BUILDINFO", buildinfo)):
        (directory / member).write_text(content, encoding="utf-8")
    shutil.copy2(archive, directory / archive.name)
    return {"name": name, "version": version, "base": base, "licenses": labels,
            "sourceUrl": url, "sourceSha256": sha(source), "signatureVerified": True,
            "recipeMatchesBuildInfo": True, "binaryPackageSha256": sha(archive),
            "sourceContainsUpstreamArchives": upstream,
            "additionalRustSources": crates,
            "gccRuntimeExceptionVerified": gcc_exception}


def main():
    parser = argparse.ArgumentParser()
    for option in ("prefix", "mpv-source", "ffmpeg-source", "output"):
        parser.add_argument("--" + option, required=True)
    args = parser.parse_args()
    prefix, output = native(args.prefix), native(args.output)
    output.mkdir(parents=True, exist_ok=True)
    runtime = output / "runtime"
    runtime.mkdir(exist_ok=True)
    bins = [prefix / "bin", native(os.environ.get("MINGW_PREFIX", "/ucrt64")) / "bin"]
    roots = list(bins[0].glob("libmpv-*.dll"))
    if len(roots) != 1:
        raise RuntimeError("Expected one newly compiled libmpv DLL")
    queue, seen, dlls, packages = roots[:], set(), [], set()
    while queue:
        path = queue.pop()
        key = path.name.lower()
        if key in seen:
            continue
        seen.add(key)
        data = run("objdump", "-p", str(path))
        if "pei-x86-64" not in data:
            raise RuntimeError("Non-x64 PE in native payload: " + path.name)
        imports = sorted(set(re.findall(r"DLL Name:\s*(\S+)", data)))
        owner = None
        if path.parent.resolve() != bins[0].resolve():
            owner = run("pacman", "-Qoq", posix(path)).strip()
            if not owner or "\n" in owner:
                raise RuntimeError("Ambiguous runtime DLL package owner")
            packages.add(owner)
        shutil.copy2(path, runtime / path.name)
        dlls.append({"file": path.name, "sha256": sha(path), "imports": imports, "package": owner,
                     "customSource": None if owner else ("mpv" if "mpv" in key else "ffmpeg")})
        for dependency in imports:
            candidate = next((b / dependency for b in bins if (b / dependency).is_file()), None)
            if candidate:
                queue.append(candidate)
            elif dependency.lower() not in SYSTEM and not dependency.lower().startswith(("api-ms-", "ext-ms-")):
                raise RuntimeError("Unresolved non-system DLL import: " + dependency)
    records = [collect_package(name, output) for name in sorted(packages)]
    for name in ("mpv", "ffmpeg"):
        source = native(getattr(args, name + "_source"))
        destination = output / "sources" / "custom" / name
        shutil.copytree(source, destination,
                        ignore=shutil.ignore_patterns(".git", "build", "*.o", "*.a", "*.dll", "*.exe", "*.pdb"))
    manifest = {"status": "candidate; parent review and nested-source audit required",
                "nativeLicenseRoute": "LGPL-compatible; no GPL/nonfree FFmpeg",
                "dlls": dlls, "packages": records,
                "bitIdenticalReproducibilityVerified": False}
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Collected {len(dlls)} native DLLs and {len(records)} matching package sources")


if __name__ == "__main__":
    main()
