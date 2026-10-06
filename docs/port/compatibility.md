# Browser compatibility evidence

## Verified graphics boundary

The recovered Stardew Valley gameplay project compiles against the browser
adapters with zero errors. The original title screen creates a farmer and loads
the farmhouse; WASD movement reaches the farm and tool use consumes original
stamina. Inventory opens, the original bed prompt accepts sleep, and the game
returns control on day 2 after writing the original farm and SaveGameInfo files.
These files are currently in the browser runtime's temporary filesystem;
reload persistence and single-player feature parity remain unverified.

Pinned environment:

- .NET SDK 10.0.401, browser target net10.0, WebAssembly components 10.0.12.
- KNI framework/platform packages 4.3.9001, browser helpers 10.0.3.
- Playwright 1.63.0 and agent-browser 0.38.2.
- Runtime interpretation; trimming, AOT and development static compression disabled.
- WebGL2/HiDef. Reach rejects the original non-power-of-two compressed font atlas.

The browser reads the actual archive content, including compressed XNBs, the
original xTile map reader, and external farm tile sheets. The xTile browser
project compiles recovered sources against KNI with an alias to the preserved
original SpriteBatch, and retains the `xTile` assembly identity.

Observed in Chromium with SwiftShader:

| Boundary | Evidence |
| --- | --- |
| Original crops texture | Loaded 256-pixel-wide atlas through the texture reader |
| Original SmallFont | 266 glyphs; 1,080 visible glyph pixels read back from a render target |
| Original farm map | Six layers, 483 tiles drawn with external tile sheets |
| SpriteBatch/render targets | 534 distinct colors read back from the rendered scene |
| Original crane-game shader | Real low-alpha pixel becomes transparent; opaque green pixel retains color and alpha |
| Keyboard | Arrow press moves the probe state through XNA Keyboard.GetState |
| Pointer | Button press is observed through XNA Mouse.GetState |
| Browser errors | No page exceptions or console errors in the passing test |

## Shader container adapter

The referenced original `Effects/ShadowRemoveMG3.8.0.xnb` contains MGFX9 OpenGL
bytecode. KNI accepts MGFX10. `LegacyEffect` expands format counters and indices,
adds the required validation tail, and retains the original GLSL, names,
samplers, render states and cache identity. It does not rebuild the shader.
Three unit tests cover a literal conversion fixture, unsupported versions and
truncation; the browser also verifies the original shader's pixel behavior.

The older `ShadowRemove.xnb` and raw `ShadowRemove.mgfxo` are not referenced by
the recovered game. Their legacy formats are not adapted. Nonempty effect
annotations are rejected explicitly; the game's referenced effect has none.

Sources: [pinned KNI package source](https://github.com/kniEngine/kni/tree/88726b31127bc3b46a3a90a2af3c2985f505c34d),
[MGFX10 reader](https://github.com/kniEngine/kni/blob/88726b31127bc3b46a3a90a2af3c2985f505c34d/src/Xna.Framework.Graphics/Graphics/Effect/MGFXReader10.cs),
[browser template](https://github.com/kniEngine/kni/tree/88726b31127bc3b46a3a90a2af3c2985f505c34d/Templates/VisualStudio2022/ProjectTemplates/BlazorGL.NetCore).
The original MGFX9 reader was inspected from the supplied MonoGame assembly.

## Reproduce

After archive preparation and recovery:

```sh
npm ci
npx playwright install --with-deps chromium
tools/check-browser.sh
python3 -m http.server 4173 --directory src/Browser/bin/Release/net10.0/publish/wwwroot
```

Open `http://localhost:4173/?diagnostic=1` for the verified graphics/audio
probe. The root page is the original game integration under development. This
serves static files only; simulation runs inside the browser. Full gameplay
startup preloads 3,559 verified non-bank files (63,914,330 encoded bytes) to
preserve synchronous original content readers. Audio waves load on demand.
Persistent saves use local IndexedDB. Lifecycle behavior and complete
single-player parity still require the remaining plan checks.

## Original renderer and new-game integration

The browser projects preserve the original four sprite renderer types and
sprite shader, packed logical/physical texture reader, and DXT decoder.
`project_graphics.py` records each original/projected source hash. The original
0.001-texel tuck and global transform behavior remain. KNI supplies GPU resources;
its native texture sorting key is retained through cached reflection. The packed
31×21 logical / 32×24 allocated texture fixture passes. Browser draw/readback
checks pass with this reader and the preserved renderer, including the original
shadow shader. A loaded-assembly check confirms the desktop MonoGame framework
is absent; the unchanged recovered Lidgren project is retargeted against KNI.

The main projection resolves texture properties using original MonoGame symbols,
adapting 481 width/height/bounds accesses while leaving unrelated dimensions
unchanged. Patches retain gameplay bodies while adapting window display helpers,
activation signature, offline SDK and shared verified content. Reflection.Emit
static holders are replaced with indexed snapshots of the original discovered
fields/default references; the round-trip test preserves boxed values, references
and nulls without generating a dynamic assembly.

The actual `GameRunner`/`Game1` lifecycle runs at the root page. The new-game
browser flow inspects original names, locations, positions, stamina, menus and
save files. It uses the original WASD/C/E/X controls and original clickable
components. The original date increment, building door action, sleep question,
overnight iterator and save serializer supply the observed transitions. No
farmer position, date or menu state is injected by this flow.

Overnight and save iterators advance over browser timer turns. Explicit yields
at the original 21 overnight barrier boundaries prevent single-player phases
from draining in one turn. Original phase bodies, local net fields and random
operations remain. Save completion retains its 100 progress marker and original
abort/fault status; invariant task culture is preserved independently of caller
culture. Individual synchronous phase/serialization costs still need the Task 7
performance measurements.

The Task 4 regression run passes 17 tooling, 22 platform, one static snapshot and
10 browser checks. A separate Chromium audit again decoded all 463 original
waves with matching original hashes/sample metadata and an idle cache maximum
of 134,164,480 bytes. Desktop execution comparisons, persistent saves, lifecycle
services and all advanced feature scenarios remain in the later plan tasks.

The Task 4 platform suite passes 22 checks, including bounded parallel asset
preload and an original-rate PCM wave stream fixture. Optional Vorbis/file-based
voices have compile support but still require browser decoder verification;
chained Vorbis remains explicitly unsupported. Complete original XACT banks were
verified separately in Task 3. None of these milestones establishes full game
or single-player feature parity.

## Browser save persistence

The store hydrates original save files and `startup_preferences` into
`/stardew-user` before the original game initializes. Each write transaction
updates a complete per-slot snapshot and retains the preceding snapshot in
IndexedDB. Original XML/zlib bytes and original `_old` files are retained.
Transactions request strict durability and resolve only after `complete`.
See the browser contracts for [transactions](https://developer.mozilla.org/en-US/docs/Web/API/IDBTransaction)
and [durability](https://developer.mozilla.org/en-US/docs/Web/API/IDBDatabase/transaction).

Original `SaveGame.Save` completes only after persistence succeeds. A failed
transaction leaves the original save menu pending, retains the previous valid
snapshot, and offers retry and pending-save export. Retry persists the captured
bytes without repeating serialization or advancing the day. Original settings
writes use the same persistence queue. Original save deletion awaits IndexedDB
deletion before removing virtual files.

The Save files panel imports a ZIP containing one original save folder or a
farm file plus `SaveGameInfo`. It validates paths, both original serialized
objects, their matching farmer/save identities, and supported game version
before committing. Import is limited to the title screen. Export downloads a
ZIP with the original files. Import supports original plain XML and legacy zlib
format; the original loader performs migration, rather than the storage layer.
Individual import files are limited to 128 MiB.

The storage checks cover exact-byte hydration/export, invalid import, aborted
and quota-limited transactions, retry gating, original mute-settings reload,
and ZIP round trips/path rejection. The real game journey preserves the initial
day-1 save on a failed day-2 transaction, exports its pending day-2 draft,
retries, reloads, imports, and uses the original Load menu. Its explicit legacy
bundle fixture removes the seventh bundle field and sets the migration cutoff
to 48; original migration restores the field. This fixture is constructed from
the original browser serializer output. It is not a genuine desktop 1.5 or
advanced-content reference save; those comparisons remain in Task 7.

Task 5 verification passes 17 tooling, 36 platform, three recovered-game and
14 browser checks. An additional run of the affected original journey confirms
normal movement after compressed import/migration and the native wake animation.
Advanced desktop fixtures, complete lifecycle/services and full feature parity
remain unverified in Tasks 6–7. These are development milestones.

## Browser lifecycle and desktop services

The original game receives native focus/activation and resize events. Losing
focus, hiding the page or opening browser save/screenshot controls releases
held keyboard and mouse buttons. Native single-player ticks and movement pause;
restored focus accepts a new press of the same movement key. Browser form input
is isolated from the framework's global input listeners. Logical mouse pixels
map through the canvas's CSS scale and letterboxing, with a minimum 1280 by 720
native game viewport so original controls fit.

The pinned KNI fullscreen methods are empty. Narrow game/window adapters now
connect original title controls and Alt+Enter to browser fullscreen. Original
windowed, borderless and exclusive preferences remain serialized; fullscreen
choices use browser fullscreen. A standard controller already connected at
startup enters KNI's normal connection handler. Synthetic controller tests cover
buttons, sticks and reconnect; physical hardware remains untested.

Original clipboard subscriber processing accepts asynchronous browser paste.
Clipboard writes and credit links use browser APIs. The text-sign paste control
requests fresh clipboard text and replaces the original textbox only while its
owning menu remains open. Distinct identical paste gestures remain distinct;
one gesture's DOM and asynchronous clipboard deliveries are deduplicated.
Original textbox width filtering is retained. Clipboard permission failures
appear outside the native interface.

Original full-map screenshot rendering, chunking, GPU readback and restoration
remain. A browser opaque RGB compositor replaces the native Skia PNG encoder.
The native farmhouse capture downloads a 192 by 192 PNG (23,097 bytes); visual
inspection shows the original room/furnishings/farmer, and subsequent normal
movement confirms control restoration. Screenshots retain their filenames in
separate local IndexedDB storage and an offline gallery, including after reload.
The native credit link calls a validated HTTP/HTTPS browser window boundary.

The combined development gate passes 17 tooling, 37 platform, three game and
23 browser checks. Final clipboard changes are verified separately in the
lifecycle evidence report. This milestone does not establish advanced gameplay
parity, desktop audio DSP equivalence, physical controller behavior or support
for other browser engines. See [lifecycle evidence](lifecycle-verification.json).
