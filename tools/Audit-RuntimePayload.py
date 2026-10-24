"""Map packaged PE files to exact restored package bytes; not a license approval."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile


def digest(data):
    return hashlib.sha256(data).hexdigest()


def audit(payload, assets_path):
    assets = json.loads(assets_path.read_text(encoding="utf-8-sig"))
    roots = [Path(p) for p in assets["packageFolders"]]
    config = json.loads((payload / "EffortLess.runtimeconfig.json").read_text(encoding="utf-8-sig"))
    runtime = next(f["version"] for f in config["runtimeOptions"]["includedFrameworks"]
                   if f["name"] == "Microsoft.NETCore.App")
    packages = [k.lower() for k, v in assets["libraries"].items() if v["type"] == "package"]
    packages.append("microsoft.netcore.app.runtime.win-x64/" + runtime)
    # Targeting/runtime packs are downloadDependencies, absent from libraries.
    for framework in assets["project"]["frameworks"].values():
        for dependency in framework.get("downloadDependencies", []):
            bounds = dependency["version"].strip("[]").split(",")
            if len(bounds) != 2 or bounds[0].strip() != bounds[1].strip():
                raise ValueError("Download dependency is not exact: " + dependency["name"])
            packages.append(dependency["name"].lower() + "/" + bounds[0].strip())
    packages = sorted(set(packages))
    files = [p for p in payload.rglob("*") if p.is_file() and p.suffix.lower() in (".dll", ".exe")]
    wanted = {p.name.lower() for p in files}
    sources = {}

    def record(name, data, package, member):
        sources.setdefault((name.lower(), digest(data)), []).append(
            {"package": package, "member": member})

    for package in packages:
        directory = next((r / package for r in roots if (r / package).is_dir()), None)
        if directory is None:
            raise ValueError("Missing restored package: " + package)
        for p in directory.rglob("*"):
            if not p.is_file():
                continue
            member = p.relative_to(directory).as_posix()
            if p.name.lower() in wanted:
                record(p.name, p.read_bytes(), package, member)
            # Only the x64 framework, never x86/ARM or unrelated SDK packages.
            if p.suffix.lower() == ".msix" and "win10-x64" in member and p.name == "Microsoft.WindowsAppRuntime.1.8.msix":
                with zipfile.ZipFile(p) as archive:
                    for entry in archive.infolist():
                        name = Path(entry.filename).name
                        if name.lower() in wanted:
                            record(name, archive.read(entry), package, member + "!/" + entry.filename)

    result = []
    for p in sorted(files):
        relative = p.relative_to(payload).as_posix()
        sha = digest(p.read_bytes())
        matches = sources.get((p.name.lower(), sha), [])
        if relative.startswith("native/win-x64/"):
            classification = "native; requires separate verified native build/source manifest"
        elif p.name in ("EffortLess.exe", "EffortLess.dll", "EffortLess.Core.dll"):
            classification = "original application build"
        elif matches:
            classification = "exact restored package bytes"
        else:
            classification = "UNMAPPED"
        result.append({"file": relative, "sha256": sha, "classification": classification, "sources": matches})
    return {"runtime": runtime, "assessment": "Byte provenance only; no compatibility or redistribution certification",
            "peFiles": result, "unmapped": [e["file"] for e in result if e["classification"] == "UNMAPPED"]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("payload", type=Path)
    parser.add_argument("--assets", type=Path, default=Path("src/EffortLess.App/obj/project.assets.json"))
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    report = audit(args.payload, args.assets)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"runtime": report["runtime"], "peFiles": len(report["peFiles"]), "unmapped": report["unmapped"]}))
    raise SystemExit(1 if report["unmapped"] else 0)
