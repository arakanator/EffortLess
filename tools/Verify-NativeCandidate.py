"""Verify native artifact integrity and vendor DLL-to-source correspondence.

Run after downloading the successful native workflow artifact. This does not
replace license review, nested/static source review or application playback QA.
Requires the platform libarchive tar utility, not a compiler or installed MSYS2.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def verify(root):
    root = root.resolve()
    count = 0
    for line in (root / 'SHA256SUMS').read_text().splitlines():
        digest, relative = line.split('  ', 1)
        path = (root / relative).resolve()
        if not path.is_relative_to(root) or sha(path) != digest:
            raise ValueError('Artifact hash/path mismatch: ' + relative)
        count += 1
    manifest = json.loads((root / 'manifest.json').read_text())
    for dll in manifest['dlls']:
        path = root / 'runtime' / dll['file']
        if path.name != dll['file'] or sha(path) != dll['sha256']:
            raise ValueError('Runtime DLL hash mismatch')
        if dll['package']:
            package = next(p for p in manifest['packages'] if p['name'] == dll['package'])
            directory = root / 'sources' / package['base']
            archives = [p for p in directory.glob('*.pkg.tar.*') if not p.name.endswith('.sig')
                        and sha(p) == package['binaryPackageSha256']]
            if len(archives) != 1:
                raise ValueError('Missing exact vendor binary package: ' + dll['file'])
            archive = archives[0]
            members = subprocess.check_output(['tar', '-tf', str(archive)], text=True).splitlines()
            members = [m for m in members if m.lower().endswith('/bin/' + dll['file'].lower())]
            if len(members) != 1:
                raise ValueError('Ambiguous packaged DLL: ' + dll['file'])
            data = subprocess.check_output(['tar', '-xOf', str(archive), members[0]])
            if hashlib.sha256(data).hexdigest() != dll['sha256']:
                raise ValueError('Installed vendor DLL differs from exact source-matched package: ' + dll['file'])
    return {'artifactFilesHashed': count, 'runtimeDllsVerified': len(manifest['dlls']),
            'vendorDllPackageBytesVerified': sum(bool(d['package']) for d in manifest['dlls']),
            'licenseAndPlaybackApproval': 'separate parent review required'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = verify(args.root)
    if args.output:
        args.output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))
