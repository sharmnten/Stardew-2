# Original content and audio preparation

The content and original-bank audio adapters are implemented. This is a
graphics/audio verification host; the original game lifecycle and complete
single-player feature checks are still pending.

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
- Sixteen tooling tests pass, including native padding, lossless conversion
  and failed-output preservation checks.
- Fourteen platform tests pass, including content integrity, all original
  cue/wave bindings, cue limits, categories, RPC pitch, voice reuse and master volume.
- Browser graphics/content checks pass: original graphics/input, the original Japanese
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

## Browser audio output

`tools/build_audio.py` converts all 463 entries to independently cached FLAC,
using FFmpeg with bit-exact 16-bit output and preserved sample rate, channels
and native decoded frame counts. Receipts include source and output hashes,
FFmpeg version and the converter source fingerprint. The prepared files total
888,852,358 bytes. Original cue/category/RPC metadata is hash checked separately.

`tools/project_xact.py` retains 29 original scheduler/parser types in a browser
namespace, changing the stream and event helper boundaries. The per-file source
identities and exact transformations are recorded in `xact-source-projection.json`.
Browser WaveBank construction creates descriptors; individual voices load waves
as needed. The original Cue, category, variation, instance-limit, looping-event
and RPC implementations control the output.

The browser verifies every downloaded wave before decoding at its original
rate. All 463 waves successfully decoded in Chromium 153. The inactive decoded
wave cache remained at or below its 134,217,728-byte limit, with an observed
maximum of 134,164,480 bytes. The largest individual wave decodes to 92,677,120
bytes. Active voices keep their buffers while playing; a transition can exceed the
idle budget until a voice releases. A real two-track browser regression verifies
that release immediately evicts inactive buffers back within the budget.
Decoding and encoded transfer buffers temporarily require memory beyond the
resident cache total.

The verification host starts suspended with zero downloaded or decoded wave
bytes. Its measured resource transfer is 30,461,892 bytes, including the
unoptimized WebAssembly framework, content/metadata manifests and selected
original assets. Verified resident content is 321,571 bytes. Playing `bigSelect`
downloads 16,055 bytes and retains 110,592 decoded wave bytes; replay reuses it.
These are verification-stage measurements, not full-game startup measurements.

Real browser checks cover gesture unlock, checksum rejection, finite/infinite
repeats, pause/resume, release, audible reverb tail and frequency filtering.
The supplied native SoundEffect implementation ignores embedded loop-region
arguments and loops its entire decoded Spring; browser output preserves that
behavior while keeping the source loop regions in the manifest.

Reverb uses a deterministic Web Audio convolution response with original XACT
settings; frequency controls use Web Audio biquad filters. These replace the
native OpenAL EAX/native filter outputs. Their exact desktop response remains
an explicit Task 7 comparison, along with in-game presentation and the remaining
browsers. No full-game or full-parity claim follows from these adapter checks.
Optional file-based audio overrides still need their game I/O integration;
no raw WAV/OGG override files are present in this archive's vanilla content.

## Commands

```sh
python3 tools/build_content.py
python3 tools/build_audio.py
tools/recover-audio.sh
dotnet run --project tools/InspectAudio -- original/Content .port-cache/audio-metadata.json
python3 tools/wave_banks.py 'original/Content/XACT/Wave Bank.xwb' .port-cache/wave-bank.json
python3 tools/wave_banks.py 'original/Content/XACT/Wave Bank(1.4).xwb' .port-cache/wave-bank-1.4.json
tools/check-browser.sh
node --test tests/browser/audio-banks.audit.mjs
```

Sources: [pinned KNI wave-bank implementation](https://github.com/kniEngine/kni/blob/88726b31127bc3b46a3a90a2af3c2985f505c34d/src/Xna.Framework.Audio/Audio/Xact/WaveBank.cs),
[browser SoundEffect implementation](https://github.com/kniEngine/kni/blob/88726b31127bc3b46a3a90a2af3c2985f505c34d/Platforms/Audio/Blazor/ConcreteSoundEffect.cs).
Original behavior was inspected directly from the supplied MonoGame assembly.
