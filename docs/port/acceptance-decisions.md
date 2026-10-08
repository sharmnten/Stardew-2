# Port decisions and deferred review observations

The original single-player implementations and all fourteen representative
acceptance groups are verified on 2026-10-08. This record preserves every
implementation ruling, its reason and stated cost if wrong. GitHub Pages deployment and live-origin acceptance are recorded separately
in the release verification report. The historical label observation was addressed by
planned release presentation; the other two minor observations remain deferred.

## Rulings

- Task 1: Ruling: prepare_archive adds an optional expected_sha256 keyword for controlled fixtures — the default is bound to the audited original archive hash — cost if wrong: accepting another archive only through an explicit programmatic override.

- Task 1: Ruling: test compiler output as the generated-source recovery regression gate — the initial full game build has already failed — cost if wrong: build success does not establish gameplay parity, which remains a separate release gate.

- Task 1: Ruling: recover with the pinned ICSharpCode.Decompiler API to select safe transforms unavailable through CLI — preserve explicit original closure methods rather than reconstruct omitted locals — cost if wrong: fidelity requires subsequent parity checks.

- Task 1: Ruling: use the conservative API only for the main game and the pinned CLI for supporting projects — support projects already compile with standard transforms — cost if wrong: further support-source defects need targeted regression fixes.

- Task 1: Ruling: generated game project name is Stardew-Valley.csproj (API output), replacing the CLI name with a space — same assembly identity — cost if wrong: scripts need the corrected path.

- Task 2: Ruling: begin the independent host test while the final recovery replay runs — all consumed desktop projects already compile — cost if wrong: replay differences could invalidate the host inputs; none occurred.

- Task 2: Ruling: compile the unchanged recovered xTile sources in a browser-specific project with the original xTile assembly identity — retargeted content readers must bind to KNI instead of the Windows MonoGame DLL — cost if wrong: browser signatures need compatibility patches.

- Task 2: Ruling: browser click test holds the button for 80ms — original XNA Mouse.GetState samples button state per frame, while Playwright zero-delay down/up can occur between frames — cost if wrong: very brief input edges need a separate lifecycle/input adapter test.

- Task 2: Ruling: disable static compression during the unoptimized compatibility stage — compressed XNB inputs and 3500 publish assets make repeated compression expensive — cost if wrong: larger development downloads; release packaging remains Task 8.

- Task 3: Ruling: build_manifest keeps the same explicit fixture hash override as prepare_archive — default bound to audited ZIP — cost if wrong: accepting other inputs only through deliberate API override.

- Task 3: Ruling: localized Japanese font expectation is 2514 glyphs, directly verified from original Japanese.fnt — the initial >3000 probe assumption was wrong — cost if wrong: reader parity would need comparison with recovered XNB XML; actual browser count matches.

- Task 3: Ruling: adapt original XACT implementation with browser output rather than KNI XACT — original CueDefinition extensions are missing and KNI browser ADPCM/streaming are unimplemented — cost if wrong: additional original audio platform adapters must be verified; no sound behavior may be silently omitted.

- Task 3: Ruling: keep native decoded block padding when preparing lossless browser audio — original WaveBank ignores declared sample trimming and SoundEffect decodes full blocks — cost if wrong: loops/durations need native decoder comparison.

- Task 3: Ruling: convert each wave to independently cached FLAC with original rate/channels/decoded frame count and external loop metadata — lossless samples and lazy audio loading avoid decoding the 500MB banks at startup — cost if wrong: browser FLAC support and audio cache limits must be tested.

- Task 3: Ruling: toolCharge RPC test sets Pitch=1200 to neutralize its second pitch curve before checking Charge — original cue references both Charge and Pitch, and updates on engine.Update — cost if wrong: future runtime-variable comparisons require both inputs.

- Task 3: Ruling: loop entire decoded waves at the browser output — supplied native SoundEffect discards loop region arguments and QueueBuffer repeats the entire Spring — cost if wrong: desktop loop comparison in Task 7; original regions remain retained.

- Task 3: Ruling: use deterministic Web Audio convolution and biquad outputs with original settings — browser has no original OpenAL EAX/native filter backend — cost if wrong: exact DSP response must be compared and corrected before Task 7 parity is marked verified.

- Task 3: Ruling: replace native audio manager thread with frame-driven voice cleanup and preserve the original scheduler — browser main thread cannot run the desktop background manager — cost if wrong: gameplay lifecycle and audio suspension checks may reveal timing differences.

- Task 4: Ruling: use compiler diagnostics as the generated-source compatibility gate before browser execution — verified recovery compiles against its native framework; KNI interface differences must be resolved explicitly — cost if wrong: compilation alone does not establish game behavior, so the real new-game flow remains required.

- Task 4: Ruling: preserve the supplied SpriteBatch/SpriteBatcher/SpriteBatchItem/SpriteEffect plus original packed texture reader and DXT decoder — KNI lacks logical image dimensions and texture tuck — cost if wrong: actual GPU/font/map/new-game tests and painted-building parity must detect differences. KNI owns physical GPU resources; logical dimensions use weak metadata. Native sorting key is read once per texture from pinned KNI without dynamic code generation.

- Task 4: Ruling: replace Reflection.Emit static holder generation with indexed reflection snapshots — retain original field discovery, default references and value boxing — cost if wrong: static save/load round-trip and gameplay transitions must establish equivalence. No local net fields removed.

- Task 4: Ruling: separate original audio output configuration from the diagnostic XACT engine — the gameplay creates and updates its own original engine; a second diagnostic engine could overwrite its reverb settings — cost if wrong: original category/RPC/gameplay audio checks must still pass.

- Task 4: Ruling: new-game flow uses WASD, as recovered Options.setControlsToDefault binds only W/A/S/D — initial movement test assumed arrow aliases and stalled at initial position — cost if wrong: movement/tool/sleep state assertions still must pass through actual input. No gameplay binding changes.

- Task 4: cooperative iterator runner fixtures RED→GREEN; 21/21 platform checks pass. Ruling: advance the original overnight/save enumerators over browser timer turns, with explicit yields at 21 original overnight barrier phase boundaries — single-player barriers are already ready and would otherwise drain in one turn — cost if wrong: actual overnight state/serialization and later desktop parity must detect sequencing changes. Original phase bodies/RNG/local net fields remain. Preserve original faulted status for CancelToTitle and stop save at its existing 100 marker. Added temporary TitleMenu click-entry diagnostics to trace persistent New-click failure; remove diagnostics with the production bridge in Task 8.

- Task 6: Ruling: finish numeric download progress and user-gesture Start in release packaging — content verification/status and audio gesture recovery work; release startup presentation still needs the approved design — cost if wrong: Task8 must not ship the current development auto-boot UI.

- Task 7: Ruling: text-sign scenario uses original Ctrl+V instead of a visible paste button — original CheckForActionOnTextSign hides pasteButton — cost if wrong: the separate replacement-callback contract remains covered by Platform38 but an inaccessible button is not treated as normal player UI.

- Task 7 journal milestone committedac12872. Fishing browser RED114423ms missing shore setup after core comparison; bridge now performs original Beach/house warps only, exposes readonly fishing state. Native first full-night RED at original LevelUpMenu: level5/2150XP fixture lacks selected profession, original Game1.fixProblems→LevelUpMenu.AddMissedProfessionChoices queues its missing level5 choice. Ruling: serialize already-selected Fisher6 as the coherent level5 prerequisite (all5 actual profession input choices independently verified); no night outcome injected and original save repair remains untouched. Shore fixture now requires adjacent original passable non-water/unoccupied tile for a reachable trap rather than merely any valid water tile. Native rerun running; production17-driver audit GREEN before this fixture-only prerequisite correction, final audit pending.

- Final: Ruling: exhaustive individual content permutations and complete arcade victory rewards — preserved original implementations plus representative play/progression/persistence satisfy approved scope — cost if wrong: a content-specific regression could remain undetected.

- Final: Ruling: every dungeon layout/live RNG trajectory — seeded generation and representative live combat accepted; unrelated random histories need not match — cost if wrong: rare layout-specific defects could remain undetected.

- Final: Ruling: physical controllers, other engines/mobile and audible hardware DSP equivalence — report available synthetic controller, Chromium and reverb/filter evidence only — cost if wrong: untested device behavior remains uncharacterized.

- Final: Ruling: genuine older advanced saves and real quota/eviction/crash behavior — constructed original migration and atomic rollback tests are scoped evidence, not universal guarantees — cost if wrong: historical-save or browser-policy failures remain possible.

- Final: Ruling: production performance/startup and Sites/deployed-origin behavior — do not claim measured or deployed acceptance until fresh production execution — cost if wrong: release remains unfinished if those gates cannot pass.

- Task8: Ruling: fixture-based complete browser suite runs the testing build; fresh production is structurally audited and tested through real Start/newfarm/sleep/durable-save/export/coldLoad controls — fixture mutation drivers are intentionally absent from production, while recovered gameplay code is identical — cost if wrong: a production-only adapter difference outside those flows could remain undetected.

- Final: Ruling: run the complete final browser glob in two ordered disjoint batches of17 and16 specs — the long tool command was terminated before completion; each bounded batch stays below that observed duration while retaining every test and the same immutable root — cost if wrong: require both zero-exit summaries and verify file-list union before accepting the gate.

- Final: Ruling: retain finalbatch1 GREEN30/30 after the museum-script-only correction and rerun allbatch2 — the first17 specs do not load the changed second-batch module; runtime/fixtures/commondriver and their sources are byteidentical — cost if wrong: an undocumented cross-test dependency would require rerunning the firstbatch too; each spec runs in its own process/context and the partition is verified.

## Review observations

- Final: minor (deferred): fingerprinted managed Steam/Galaxy wrappers bypass literal-filename dependency audit; these are dormant browser assembly containers, not native runtime loads.

- Final: minor (deferred): detailed readonly GameSnapshot/LastTitleClick remain on production frames; performance cost not measured.

- Final: minor (deferred): graphics/development labels remain until all acceptance checks pass; final acceptance status must be honest.

The third historical observation is addressed by planned release presentation.
The fingerprinted-wrapper audit and snapshot performance observations remain
deferred.

Ruling: Replace the planned private Sites deployment with this repository's
GitHub Pages deployment — the user explicitly requested GitHub Pages after
local acceptance. Publish the exact audited production output as a release
archive, verify its SHA-256 in Actions, and deploy through the supported Pages
artifact workflow; preserve the original game/runtime bytes — if wrong, an
incorrect archive or project-site path could prevent the game from starting.

Ruling: Preserve the original final browser evidence while adding deployed-origin
routing to the test harness — only the initial navigation URL changes, and the
original startup/production save assertions run unchanged against the Pages
origin. No shipped runtime or original gameplay implementation changes — if
wrong, local acceptance could be mistaken for live-origin acceptance; record
the latter only after its separate run passes.

Ruling: Retry temporary static-host failures without changing original asset
identity or gameplay — live Pages traces returned Fastly/Varnish HTTP 503 for
valid content files, while direct requests returned the original bytes. Shared
content/audio/JSON downloads make at most four attempts with 250/500/1000 ms
cancellation-aware backoff for 408/429/502/503/504. Permanent errors and checksum
failures remain actionable — if wrong, backoff could delay loading or mask a
real missing asset; native cancellation/permanent-error/checksum checks and
production browser checks cover those boundaries.

Ruling: Accept a browser resource warning only after the same URL's latest
request finishes successfully — Chromium logs the first handled 503 even when
the game retries and verifies the original bytes. Keep all page exceptions,
game logger errors and unresolved resource failures fatal — if wrong, a runtime
failure could be mistaken for recovered transport; the regression requires an
actual ready game and exactly two requests after one injected 503.

Ruling: Scope new verification to the changed static transport and published
origin — the full 66-check gameplay corpus and 33 desktop/reference comparisons
precede this transport fix; original gameplay implementations, content identity
and save serialization do not change. Run all 47 platform checks, production
startup/503 recovery/overnight save/export/cold Load, and the separate live-origin
flows — if wrong, a transport-only change could affect an unexercised game path;
the original adapters and checksum contracts remain covered by the complete
platform suite and production journey.
