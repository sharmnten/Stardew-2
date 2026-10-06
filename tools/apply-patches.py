#!/usr/bin/env python3
"""Project the verified recovery baseline; apply only recorded browser patches."""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess
from build_content import ROOT

def main():
    baseline = ROOT / 'src/Recovered/Game'
    output = ROOT / '.port-cache/game-browser'
    output.mkdir(parents=True, exist_ok=True)
    sources = sorted(path for path in baseline.rglob('*.cs') if path.relative_to(baseline).parts[0] not in ('bin', 'obj'))
    inputs = sources + [ROOT / path for path in ['patches/game-browser.patch', 'tools/apply-patches.py',
        'tools/ProjectGame/Program.cs', 'tools/ProjectGame/ProjectGame.csproj', 'global.json']]
    inputs += sorted((ROOT / 'original').glob('*.dll'))
    input_hashes = {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in inputs}
    receipt = output / '.projection.json'
    if receipt.exists():
        cached = json.loads(receipt.read_text())
        actual = {str(path.relative_to(output)): hashlib.sha256(path.read_bytes()).hexdigest() for path in output.rglob('*.cs')}
        if cached.get('inputs') == input_hashes and cached.get('outputs') == actual:
            print(f'Verified cached browser projection of {len(actual)} gameplay files.')
            return
    retained = set()
    for source in sources:
        relative = source.relative_to(baseline)
        if relative.parts[0] in ('bin', 'obj'): continue
        target = output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        retained.add(relative)
        if not target.exists() or hashlib.sha256(source.read_bytes()).digest() != hashlib.sha256(target.read_bytes()).digest():
            shutil.copyfile(source, target)
    for target in output.rglob('*.cs'):
        if target.relative_to(output) not in retained: target.unlink()
    patch = ROOT / 'patches/game-browser.patch'
    if patch.exists():
        with patch.open('rb') as stream:
            subprocess.run(['patch', '--batch', '--forward', '--fuzz=0', '-p1'], cwd=output, stdin=stream, check=True)
    subprocess.run(['dotnet', 'run', '--project', str(ROOT / 'tools/ProjectGame/ProjectGame.csproj'),
                    '-c', 'Release', '--', str(output), str(ROOT / 'original')], check=True)
    output_hashes = {str(path.relative_to(output)): hashlib.sha256(path.read_bytes()).hexdigest() for path in output.rglob('*.cs')}
    receipt.write_text(json.dumps({'schema': 1, 'inputs': input_hashes, 'outputs': output_hashes}, indent=2) + '\n')
    print(f'Projected {len(retained)} recovered gameplay files from the verified desktop baseline.')

if __name__ == '__main__':
    main()
