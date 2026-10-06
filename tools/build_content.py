#!/usr/bin/env python3
"""Validate extracted original content and write its deterministic manifest."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import tempfile

ROOT = Path(__file__).resolve().parents[1]
EXPECTED_SHA256 = json.loads((ROOT / 'docs/port/archive-inventory.json').read_text())['sha256']


def build_manifest(original: Path, output: Path, *, expected_sha256=EXPECTED_SHA256) -> dict:
    audit = json.loads((original / '.archive-manifest.json').read_text())
    if audit['archive_sha256'] != expected_sha256:
        raise ValueError('Extracted archive SHA-256 does not match the audited input')
    entries = []
    names = set()
    expected_paths = {name for name in audit['file_sha256'] if name.startswith('Content/')}
    for path in sorted(expected_paths):
        parts = PurePosixPath(path)
        if parts.is_absolute() or '..' in parts.parts or '\\' in path:
            raise ValueError(f'Invalid content path: {path}')
        file = original / path
        if not file.is_file():
            raise FileNotFoundError(f'Missing original content: {path}')
        if file.is_symlink() or not file.resolve().is_relative_to(original.resolve()):
            raise ValueError(f'Content path escapes the extraction: {path}')
        with file.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        if digest != audit['file_sha256'][path]:
            raise ValueError(f'Original content checksum mismatch: {path}')
        name = path[len('Content/'):]
        if name.endswith('.xnb'):
            name = name[:-4]
        if name.casefold() in names:
            raise ValueError(f'Asset name collision: {name}')
        names.add(name.casefold())
        group = 'audio-bank' if file.suffix.lower() == '.xwb' else 'audio-metadata' if name.startswith('XACT/') else 'content'
        entries.append({'name': name, 'path': path, 'sha256': digest, 'size': file.stat().st_size, 'group': group})
    actual_paths = {file.relative_to(original).as_posix() for file in (original / 'Content').rglob('*') if file.is_file()}
    if actual_paths != expected_paths:
        raise ValueError(f'Unindexed original content: {sorted(actual_paths - expected_paths)}')
    manifest = {'schema': 1, 'game_version': '1.6.15.24356', 'archive_sha256': expected_sha256, 'assets': entries}
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile('w', dir=output.parent, delete=False, encoding='utf-8') as stream:
            temporary = Path(stream.name)
            json.dump(manifest, stream, indent=2, ensure_ascii=False)
            stream.write('\n')
        os.replace(temporary, output)
    finally:
        if temporary and temporary.exists():
            temporary.unlink()
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original', type=Path, default=ROOT / 'original')
    parser.add_argument('--output', type=Path, default=ROOT / '.port-cache/content/manifest.json')
    args = parser.parse_args()
    result = build_manifest(args.original, args.output)
    print(f"Verified {len(result['assets'])} original content files; manifest: {args.output}")
