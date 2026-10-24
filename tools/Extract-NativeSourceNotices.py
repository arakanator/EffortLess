"""Retain copyright/license documents from already verified native source archives.

This extracts notices; signature/source correspondence must be verified separately.
Use Python 3 and the platform's libarchive `tar` command for MSYS2 .src.tar.zst.
"""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import tarfile
import tempfile
import zipfile


def sha(data):
    return hashlib.sha256(data).hexdigest()


def wanted(member):
    path = PurePosixPath(member)
    return (any(p.lower() in ("licenses", "licences", "license", "licence") for p in path.parts[:-1])
            or bool(re.match(r"(?i)^(licen[cs]e|copying|copyright|notice)([._-]|$)", path.name)))


def extract(root, output):
    output.mkdir(parents=True, exist_ok=True)
    records = []
    for source in sorted(root.rglob("*.src.tar.zst")):
        source_hash = hashlib.file_digest(source.open("rb"), "sha256").hexdigest()
        family = source.name.removesuffix(".src.tar.zst")

        def save(member, data, inner=None):
            # Hash the complete member identity to preserve distinct same-name
            # notices while avoiding long Windows paths or archive traversal.
            identity = (inner or "") + "!/" + member
            leaf = re.sub(r"[^A-Za-z0-9._-]", "_", PurePosixPath(member).name)[:90]
            destination = output / family / (sha(identity.encode())[:16] + "-" + leaf)
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
            records.append({"file": destination.relative_to(output).as_posix(), "sha256": sha(data),
                            "sourceArchive": source.name, "sourceArchiveSha256": source_hash,
                            "upstreamArchiveMember": inner, "noticeMember": member})

        entries = subprocess.check_output(["tar", "-tf", str(source)], text=True).splitlines()
        for entry in entries:
            if entry.endswith("/"):
                continue
            if wanted(entry):
                save(entry, subprocess.check_output(["tar", "-xOf", str(source), entry]))
            elif entry.endswith((".tar.gz", ".tgz", ".tar.xz", ".tar.bz2", ".tar", ".zip")):
                with tempfile.TemporaryDirectory() as scratch:
                    nested = Path(scratch) / "upstream"
                    with nested.open("wb") as stream:
                        subprocess.run(["tar", "-xOf", str(source), entry], stdout=stream, check=True)
                    if entry.endswith(".zip"):
                        with zipfile.ZipFile(nested) as archive:
                            for item in archive.infolist():
                                if not item.is_dir() and wanted(item.filename):
                                    save(item.filename, archive.read(item), entry)
                    else:
                        with tarfile.open(nested) as archive:
                            for item in archive:
                                if item.isfile() and wanted(item.name):
                                    save(item.name, archive.extractfile(item).read(), entry)
    (output / "SOURCE-NOTICES.json").write_text(json.dumps(records, indent=2) + "\n", encoding="utf-8")
    return records


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    print("Source notice documents retained:", len(extract(args.root, args.output)))
