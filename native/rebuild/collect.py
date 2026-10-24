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
        raise RuntimeError(f"Command failed: {args[0]} {args[1:]}\n{result.stdout[-3000:]}\n{result.stderr[-3000:]}")
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


def verify_source(source):
    # Equivalent trust requirements to pacman-key: require a valid signature
    # AND full/ultimate trust in pacman's existing, populated MSYS2 keyring.
    status = run(str(native('/usr/bin/gpg.exe')), '--homedir', '/etc/pacman.d/gnupg',
                 '--batch', '--no-permission-warning', '--status-fd', '1', '--verify',
                 posix(str(source) + '.sig'), posix(source))
    if ('[GNUPG:] VALIDSIG ' not in status or
            not re.search(r'\[GNUPG:\] TRUST_(FULLY|ULTIMATE)', status) or
            re.search(r'REVKEYSIG|EXPKEYSIG|EXPSIG|BADSIG', status)):
        raise RuntimeError('Source signature lacks trusted validity: ' + source.name + '\n' + status)


SYSTEM = {n + ".dll" for n in (
    "advapi32 avrt bcrypt bcryptprimitives cfgmgr32 combase comctl32 comdlg32 crypt32 cryptbase "
    "d3d9 d3d10 d3d10_1 d3d11 d3d12 d3dcompiler_47 dcomp dinput8 dnsapi dsound dwmapi dwrite "
    "dxgi dxva2 gdi32 gdi32full glu32 hid imm32 iphlpapi kernel32 kernelbase mf mfplat "
    "mfreadwrite mpr msacm32 msasn1 msvcrt msvfw32 ncrypt netapi32 normaliz ntdll ole32 "
    "oleaut32 opengl32 powrprof propsys psapi rpcrt4 secur32 setupapi shell32 shlwapi "
    "ucrtbase user32 userenv uxtheme version win32u winhttp winmm wintrust wintypes "
    "wldap32 ws2_32 wtsapi32"
).split()}
SYSTEM.update({"cabinet.dll", "clbcatq.dll", "d2d1.dll", "dbghelp.dll", "imagehlp.dll",
               "mfuuid.dll", "mmdevapi.dll", "msvcp_win.dll", "oleacc.dll", "usp10.dll",
               "wininet.dll", "winspool.drv", "urlmon.dll", "windowscodecs.dll", "shcore.dll",
               "bcp47langs.dll", "coremessaging.dll", "mscms.dll", "uiautomationcore.dll",
               "xmllite.dll", "avicap32.dll", "dxcore.dll"})


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


def collect_package(name, output, shipped):
    version = run("pacman", "-Q", name).strip().split()[1]
    archive, info, text = package_archive(name, version, native("/var/cache/pacman/pkg"))
    if "ffmpeg" in name:
        raise RuntimeError("Prebuilt GPL FFmpeg must not enter LGPL payload: " + name)
    base = info["pkgbase"][0]
    buildinfo = run("bsdtar", "-xOf", str(archive), ".BUILDINFO")
    build = fields(buildinfo)
    directory = output / "sources" / base
    directory.mkdir(parents=True, exist_ok=True)
    # MSYS2 renders an epoch as '~' in Windows-safe archive filenames.
    filename = base + "-" + version.replace(":", "~") + ".src.tar.zst"
    source = directory / filename
    url = "https://mirror.msys2.org/mingw/sources/" + filename
    for suffix in ("", ".sig"):
        run("curl", "--fail", "--location", "--retry", "2", "--proto", "=https",
            "--proto-redir", "=https", "--output", str(source) + suffix, url + suffix)
    verify_source(source)
    entries = run("bsdtar", "-tf", str(source)).splitlines()
    recipe = next((e for e in entries if Path(e).name == "PKGBUILD"), None)
    srcinfo = next((e for e in entries if Path(e).name == ".SRCINFO"), None)
    if not recipe or not srcinfo:
        raise RuntimeError("Source archive lacks recipe/metadata: " + filename)
    recipe_bytes = subprocess.check_output(["bsdtar", "-xOf", str(source), recipe])
    expected_recipe = build.get("pkgbuild_sha256sum", [])
    if len(expected_recipe) != 1 or hashlib.sha256(recipe_bytes).hexdigest() != expected_recipe[0]:
        raise RuntimeError("Source recipe does not match binary BUILDINFO: " + filename)
    upstream = [e for e in entries if e.endswith((".tar.gz", ".tar.xz", ".tar.bz2", ".tar.lz", ".tar.zst", ".tgz", ".zip", ".tar"))]
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
        # Some MSYS2 recipes omit share/licenses even when upstream supplies
        # complete notices. Recover those bytes from the signature-verified,
        # recipe-matched upstream source; never invent substitute license text.
        for entry in upstream:
            with tempfile.TemporaryDirectory() as scratch:
                nested = Path(scratch) / 'upstream'
                nested.write_bytes(subprocess.check_output(['bsdtar', '-xOf', str(source), entry]))
                for member in run('bsdtar', '-tf', str(nested)).splitlines():
                    leaf = Path(member).name
                    if not member.endswith('/') and re.match(r'(?i)^(licen[cs]e|copying|copyright|notice|patents)([._-]|$)', leaf):
                        identity = hashlib.sha256((entry + '!/' + member).encode()).hexdigest()[:16]
                        destination = output / 'licenses' / name / (identity + '-' + leaf)
                        destination.parent.mkdir(parents=True, exist_ok=True)
                        destination.write_bytes(subprocess.check_output(['bsdtar', '-xOf', str(nested), member]))
                        licenses.append(destination)
        if not licenses:
            raise RuntimeError("No upstream copyright/license files for runtime package: " + name)
    labels = info.get("license", [])
    evidence = "\n".join(p.read_text(encoding="utf-8", errors="replace") for p in licenses)
    gcc_exception = base == 'mingw-w64-gcc' and "GCC RUNTIME LIBRARY EXCEPTION" in evidence.upper()
    component_route = None
    # Package labels also cover command-line tools and optional GPL plugins.
    # Assess only an explicit DLL allowlist, retaining every package notice.
    reviewed = {
        'mingw-w64-gettext': ({'libintl-8.dll', 'libasprintf-0.dll'},
            '7ef2cdfe58e0c0460657b6598b49af29d4e03c1e41cbaf0e1da1eb8ad74b95d0', 'LGPL-2.1-or-later libraries; GPL programs excluded'),
        'mingw-w64-libiconv': ({'libiconv-2.dll', 'libcharset-1.dll'},
            '4ae66e61d1c6593c84c47105b368d579bcee1af566dbb959ec63a0c48425dcf4', 'LGPL-2.1-or-later libraries; GPL program/documentation excluded'),
        'mingw-w64-lcms2': ({'liblcms2-2.dll'},
            '6dbd60437f8ef91d8de1f08ad75882547fd4931bfcc3566a0735f28db1484d31', 'MIT core; separate GPL fast-float/threaded plugins excluded'),
    }
    if base in reviewed:
        allowed, digest, component_route = reviewed[base]
        if not shipped or not set(shipped) <= allowed or not any(sha(p) == digest for p in licenses):
            raise RuntimeError('Component license assessment no longer matches: ' + name)
    custom_hashes = {
        'mingw-w64-fontconfig': '51a51aa9823704fd90bccc616cdd17ebabb5b2b3e9cbde886ca02c7002288067',
        'mingw-w64-libvorbis': 'ec1815db59fcd302846df949d7424876cb2e2dc5ed1606c5fb0b36787b1cf43a',
    }
    reviewed_custom = base in custom_hashes and any(sha(p) == custom_hashes[base] for p in licenses)
    if base == 'mingw-w64-uchardet':
        # Verify the actual alternative-license grant, not MPL-1.1 alone.
        archive_member = next(e for e in upstream if e.endswith('uchardet-0.0.8.tar.xz'))
        with tempfile.TemporaryDirectory() as scratch:
            nested = Path(scratch) / 'uchardet.tar.xz'
            nested.write_bytes(subprocess.check_output(['bsdtar', '-xOf', str(source), archive_member]))
            with tarfile.open(nested) as archive:
                grant = archive.extractfile('uchardet-0.0.8/src/uchardet.cpp').read()
        if hashlib.sha256(grant).hexdigest() != 'f4bc95e2b06032e718e76aefe5b10d5a27103046304af480673f926a2364bf45':
            raise RuntimeError('uchardet LGPL alternative grant changed')
        component_route = 'LGPL-2.1-or-later alternative, verified upstream source header'
    bzip2_license = name.endswith('-bzip2') and any(
        sha(p) == 'c6dbbf828498be844a89eaa3b84adbab3199e342eb5cb2ed2f0d4ba7ec0f38a3' for p in licenses)
    for label in labels:
        upper = label.upper()
        # OR permits selecting a compatible branch; AND with GPL-only does not.
        if re.search(r"(?<!L)GPL", upper) and not gcc_exception and not component_route:
            if not (" OR " in upper and " AND " not in upper and re.search(r"MIT|BSD|APACHE|ISC|LGPL|FTL", upper)):
                raise RuntimeError("GPL-only package needs component-level assessment: " + name + " " + label)
        if not re.search(r"LGPL|GPL|MIT|BSD|APACHE|ISC|ZLIB|FTL|MPL|X11|UNICODE|CC0|BSL|UNLICENSE|PUBLIC", upper):
            if not ((upper == "CUSTOM" and "Permission is hereby granted" in evidence) or
                    (gcc_exception and "GFDL" in upper) or (upper == 'CUSTOM' and (bzip2_license or reviewed_custom))):
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
            "gccRuntimeExceptionVerified": gcc_exception,
            "shippedDlls": sorted(shipped), "componentLicenseAssessment": component_route}


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
    # Collect every package's actual license evidence up front. A first failing
    # label must not hide the remaining licenses from the parent review.
    diagnostic = output.parent / 'records'
    diagnostic.mkdir(exist_ok=True)
    probes = {}
    for name in sorted(packages):
        version = run('pacman', '-Q', name).strip().split()[1]
        archive, info, text = package_archive(name, version, native('/var/cache/pacman/pkg'))
        probes[name] = {'info': info, 'buildInfo': fields(run('bsdtar', '-xOf', str(archive), '.BUILDINFO'))}
        for row in run('pacman', '-Ql', name).splitlines():
            member = row.partition(' ')[2]
            if '/share/licenses/' in member:
                path = native(member)
                if path.is_file():
                    destination = diagnostic / 'native-package-notices' / name / member.split('/share/licenses/', 1)[1]
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(path, destination)
    (diagnostic / 'native-package-metadata.json').write_text(json.dumps(probes, indent=2)+'\n', encoding='utf-8')
    records = [collect_package(name, output, [d['file'].lower() for d in dlls if d['package'] == name]) for name in sorted(packages)]
    for name in ("mpv", "ffmpeg"):
        source = native(getattr(args, name + "_source"))
        destination = output / "sources" / "custom" / (name + ".tar")
        destination.parent.mkdir(parents=True, exist_ok=True)
        # Archive the exact Git input; do not follow generated build symlinks or
        # accidentally collect object files. Generated configuration is in records.
        run("git", "-C", str(source), "diff", "--exit-code")
        with destination.open("wb") as stream:
            subprocess.run(["git", "-C", str(source), "archive", "--format=tar",
                            "--prefix=" + name + "/", "HEAD"], stdout=stream, check=True)
    manifest = {"status": "candidate; parent review and nested-source audit required",
                "nativeLicenseRoute": "LGPL-compatible; no GPL/nonfree FFmpeg",
                "dlls": dlls, "packages": records,
                "bitIdenticalReproducibilityVerified": False}
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Collected {len(dlls)} native DLLs and {len(records)} matching package sources")


if __name__ == "__main__":
    main()
