# Original content and audio preparation

Task 3 is in progress. Content integrity and custom readers pass; browser audio
playback and complete audio adaptation are not verified yet.

`tools/build_content.py` validates every extracted content file against the
archive's per-file hashes and emits a deterministic manifest. Original names,
paths, hashes and sizes remain unchanged. The manifest separates ordinary
content, audio metadata and audio banks. It rejects missing or changed content,
case collisions and unindexed files before publishing a manifest.

`BrowserContentStore` preloads and verifies bytes before exposing read-only
streams to synchronous game readers. It preserves Windows-style path aliases
inside the content root, prevents escaping it, and serializes overlapping
preloads so a resident asset is stored once. It checks the game version and
archive identity before downloading content.

Verified so far:

- All 3,561 archive content files pass checksum validation.
- Twelve tooling tests pass, including four wave-bank boundary tests.
- Nine platform tests pass, including checksum rejection, missing assets,
  path normalization, unloaded-stream rejection and overlapping loads.
- Three browser tests pass: original graphics/input, the original Japanese
  BmFont reader with all 2,514 glyphs, and a deliberately corrupted farm map
  rejected by identity before decoding.

## Audio evidence

The original MonoGame assembly's AudioEngine and SoundBank constructors can
parse XGS/XSB metadata without initializing playback. `tools/InspectAudio`
exports their metadata using the original parser. Its small input copies live
in ignored build output because the original TitleContainer refuses absolute
paths and removes parent-directory segments.

The original parser reads 433 cues, six categories and 11 RPC curves. All 463
wave references resolve to the original two banks: 437 entries in Wave Bank
and 26 in Wave Bank(1.4). There are 457 Microsoft ADPCM entries and six PCM
entries. All entries are referenced; no bank content can be omitted.

The original event definitions include 102 indefinitely looping wave events,
237 single-play events and one finite repeat event. Cue instance limits,
variation weights, pitch/volume ranges, category assignments, RPC variables
and reverb settings are retained in the recovered source and original metadata.
See `audio-inventory.json` for the audited identities and counts.

KNI's browser XACT ADPCM initialization is empty and its streaming wave-bank
path throws NotImplementedException. Its API also lacks the supplied game's
custom CueDefinition extensions. This requires the original XACT implementation
with browser audio outputs, rather than directly using KNI's XACT playback.

`tools/recover-audio.sh` reproducibly recovers the original audio namespace into
ignored `src/Recovered/Xact/`. `tools/wave_banks.py` inspects bank entries without
loading or decoding the whole bank. It retains source ranges, rates, channels,
format bits, declared and native decoded sample counts, and loop points. The
native decoded count includes compressed-block padding, matching the supplied
SoundEffect initialization; declaring a shorter XACT duration does not trim
that padding in the original implementation.

## Commands

```sh
python3 tools/build_content.py
tools/recover-audio.sh
dotnet run --project tools/InspectAudio -- original/Content .port-cache/audio-metadata.json
python3 tools/wave_banks.py 'original/Content/XACT/Wave Bank.xwb' .port-cache/wave-bank.json
python3 tools/wave_banks.py 'original/Content/XACT/Wave Bank(1.4).xwb' .port-cache/wave-bank-1.4.json
```

Sources: [pinned KNI wave-bank implementation](https://github.com/kniEngine/kni/blob/88726b31127bc3b46a3a90a2af3c2985f505c34d/src/Xna.Framework.Audio/Audio/Xact/WaveBank.cs),
[browser SoundEffect implementation](https://github.com/kniEngine/kni/blob/88726b31127bc3b46a3a90a2af3c2985f505c34d/Platforms/Audio/Blazor/ConcreteSoundEffect.cs).
Original behavior was inspected directly from the supplied MonoGame assembly.
