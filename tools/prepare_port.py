#!/usr/bin/env python3
"""Verify the reference ZIP and extract it without changing the input."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import stat
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
EXPECTED_SHA256 = json.loads((ROOT / 'docs/port/archive-inventory.json').read_text())['sha256']


def prepare_archive(archive: Path, output: Path, *, expected_sha256: str = EXPECTED_SHA256) -> dict:
    archive, output = Path(archive), Path(output).absolute()
    if output.exists() or output.is_symlink():
        raise FileExistsError(f'Extraction already exists: {output}')
    with archive.open('rb') as source:
        digest = hashlib.file_digest(source, 'sha256').hexdigest()
    if digest != expected_sha256:
        raise ValueError(f'Archive SHA-256 mismatch: expected {expected_sha256}, got {digest}')

    with zipfile.ZipFile(archive) as z:
        members = []
        names = set()
        for info in z.infolist():
            path = PurePosixPath(info.filename)
            if (path.is_absolute() or '\\' in info.filename
                    or '..' in path.parts or not path.parts
                    or path.parts[0] != 'Stardew Valley'
                    or stat.S_ISLNK(info.external_attr >> 16)):
                raise ValueError(f'Unsafe archive path: {info.filename}')
            if len(path.parts) == 1:
                if info.is_dir():
                    continue
                raise ValueError(f'Unexpected archive root: {info.filename}')
            relative = PurePosixPath(*path.parts[1:])
            if relative.as_posix() in names:
                raise ValueError(f'Duplicate archive path: {info.filename}')
            names.add(relative.as_posix())
            members.append((info, relative))

        output.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='.extract-', dir=output.parent) as temporary:
            staging = Path(temporary) / 'game'
            staging.mkdir()
            file_hashes = {}
            for info, relative in members:
                destination = staging.joinpath(*relative.parts)
                if info.is_dir():
                    destination.mkdir(parents=True, exist_ok=True)
                    continue
                destination.parent.mkdir(parents=True, exist_ok=True)
                with z.open(info) as source, destination.open('wb') as target:
                    shutil.copyfileobj(source, target, 1024 * 1024)
                with destination.open('rb') as source:
                    file_hashes[relative.as_posix()] = hashlib.file_digest(source, 'sha256').hexdigest()
            report = {'archive_sha256': digest, 'files': len(file_hashes),
                      'game_directory': str(output), 'file_sha256': dict(sorted(file_hashes.items()))}
            (staging / '.archive-manifest.json').write_text(json.dumps(report, indent=2) + '\n')
            if output.exists() or output.is_symlink():
                raise FileExistsError(f'Extraction appeared during preparation: {output}')
            os.rename(staging, output)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, default=ROOT / 'Stardew Valley.zip')
    parser.add_argument('--output', type=Path, default=ROOT / 'original')
    args = parser.parse_args()
    report = prepare_archive(args.archive, args.output)
    print(json.dumps({key: value for key, value in report.items() if key != 'file_sha256'}, indent=2))


if __name__ == '__main__':
    main()
