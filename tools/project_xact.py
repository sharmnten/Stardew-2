#!/usr/bin/env python3
"""Project the original XACT scheduler into the browser output namespace.

Only namespaces and two platform helper calls change here. Native bank decoding
and native voice output are supplied by the separately reviewed browser boundary.
"""
from pathlib import Path
import hashlib
import json
from build_content import ROOT

NAMES = '''AudioEngine AudioCategory Cue CueDefinition SoundBank XactSoundBankSound
XactClip ClipEvent PlayWaveEvent PlayWaveVariant VolumeEvent RpcCurve RpcPoint
RpcPointType RpcParameter RpcVariable ReverbSettings DspParameter XactHelpers
VariationType CrossfadeType MaxInstanceBehavior FilterMode AudioStopOptions
SoundState AudioListener AudioEmitter InstancePlayLimitException
SoundEffectInstancePool'''.split()

def main():
    source = ROOT / 'src/Recovered/Xact/Microsoft.Xna.Framework.Audio'
    destination = ROOT / '.port-cache/xact-browser'
    destination.mkdir(parents=True, exist_ok=True)
    files = []
    for name in NAMES:
        body = (source / (name + '.cs')).read_text()
        body = '#nullable disable\nusing Microsoft.Xna.Framework;\n' + body.replace(
            'namespace Microsoft.Xna.Framework.Audio;',
            'namespace StardewBrowser.Platform.Audio.Xact;')
        body = body.replace('TitleContainer.OpenStream(filePath)', 'XactRuntime.OpenStream(filePath)')
        body = body.replace('EventHelpers.Raise(', 'XactRuntime.Raise(')
        output = destination / (name + '.cs')
        if not output.exists() or output.read_text() != body:
            output.write_text(body)
        files.append({'name': name, 'original_sha256': hashlib.sha256((source / (name + '.cs')).read_bytes()).hexdigest(),
                      'projected_sha256': hashlib.sha256(output.read_bytes()).hexdigest()})
    report = {'schema': 1, 'namespace': 'StardewBrowser.Platform.Audio.Xact',
              'changes': ['namespace and parent XNA math import', 'nullable context disabled for recovered source',
                          'TitleContainer.OpenStream → XactRuntime.OpenStream', 'EventHelpers.Raise → XactRuntime.Raise'],
              'files': files}
    (destination / 'source-projection.json').write_text(json.dumps(report, indent=2) + '\n')
    print(f'Projected {len(NAMES)} original XACT scheduler types; native output remains separate.')

if __name__ == '__main__':
    main()
