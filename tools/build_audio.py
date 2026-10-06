#!/usr/bin/env python3
"""Prepare independently cached lossless waves and retain original cue metadata."""
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
from build_content import ROOT, build_manifest
from wave_banks import inspect_bank, encode_flac


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile('w', dir=path.parent, delete=False, encoding='utf-8') as stream:
        staging = Path(stream.name)
        json.dump(value, stream, indent=2, ensure_ascii=False)
        stream.write('\n')
    try:
        os.replace(staging, path)
    finally:
        staging.unlink(missing_ok=True)


def main():
    original = ROOT / 'original'
    output = ROOT / '.port-cache/audio'
    output.mkdir(parents=True, exist_ok=True)
    content = build_manifest(original, ROOT / '.port-cache/content/manifest.json')
    subprocess.run(['dotnet', 'run', '--project', str(ROOT / 'tools/InspectAudio'), '--',
                    str(original / 'Content'), str(output / 'cues.json')], cwd=ROOT, check=True)
    metadata = json.loads((output / 'cues.json').read_text())
    files = [original / 'Content/XACT' / (name + '.xwb') for name in metadata['waveBankNames']]
    banks = [inspect_bank(path) for path in files]
    audited = {entry['path']: entry['sha256'] for entry in content['assets']}
    for path, bank in zip(files, banks):
        if bank['sha256'] != audited[path.relative_to(original).as_posix()]:
            raise ValueError('Wave bank changed during audio preparation: ' + bank['name'])
    recipe = subprocess.run(['ffmpeg', '-version'], capture_output=True, text=True, check=True).stdout.splitlines()[0]
    recipe += '; converter-sha256=' + hashlib.sha256((ROOT / 'tools/wave_banks.py').read_bytes()).hexdigest()

    def prepare(job):
        bank_index, entry = job
        with files[bank_index].open('rb') as source:
            source.seek(entry['offset'])
            data = source.read(entry['size'])
        source_hash = hashlib.sha256(data).hexdigest()
        relative = f'{bank_index}/{entry["index"]:03}.flac'
        destination = output / relative
        receipt_path = output / '.receipts' / (relative.replace('/', '-') + '.json')
        converted = None
        if receipt_path.exists() and destination.exists():
            try:
                receipt = json.loads(receipt_path.read_text())
                with destination.open('rb') as source:
                    digest = hashlib.file_digest(source, 'sha256').hexdigest()
                if receipt['source_sha256'] == source_hash and receipt['recipe'] == recipe and receipt['converted']['sha256'] == digest:
                    converted = receipt['converted']
            except (ValueError, KeyError):
                pass
        if converted is None:
            converted = encode_flac(entry, data, destination)
            write_json(receipt_path, {'source_sha256': source_hash, 'recipe': recipe, 'converted': converted})
        return {'bank': bank_index, 'bank_name': banks[bank_index]['name'], 'track': entry['index'],
                'path': 'Audio/' + relative, 'source_sha256': source_hash, 'original': entry, **converted}

    jobs = [(index, entry) for index, bank in enumerate(banks) for entry in bank['entries']]
    waves = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as workers:
        for wave in workers.map(prepare, jobs):
            waves.append(wave)
            if len(waves) % 20 == 0 or len(waves) == len(jobs):
                print(f'Prepared {len(waves)}/{len(jobs)} original waves', flush=True)

    references = set()

    def inspect_refs(value):
        if isinstance(value, dict):
            kind, fields = value.get('type'), value.get('fields', {})
            if kind == 'PlayWaveVariant': references.add((fields['waveBank'], fields['track']))
            if kind == 'XactSoundBankSound' and not fields['complexSound']: references.add((fields['waveBankIndex'], fields['trackIndex']))
            for child in value.values(): inspect_refs(child)
        elif isinstance(value, list):
            for child in value: inspect_refs(child)

    inspect_refs(metadata['cues'])
    if references != {(wave['bank'], wave['track']) for wave in waves}:
        raise ValueError('Original cue references do not match the prepared wave entries')
    report = {'schema': 1, 'game_version': content['game_version'], 'archive_sha256': content['archive_sha256'],
              'recipe': recipe, 'cue_count': metadata['cueCount'], 'metadata_path': 'Audio/cues.json',
              'metadata_sha256': hashlib.sha256((output / 'cues.json').read_bytes()).hexdigest(),
              'banks': banks, 'waves': waves}
    write_json(output / 'manifest.json', report)
    print(f'Prepared all {len(waves)} lossless waves and validated all {metadata["cueCount"]} original cues.', flush=True)


if __name__ == '__main__':
    main()
