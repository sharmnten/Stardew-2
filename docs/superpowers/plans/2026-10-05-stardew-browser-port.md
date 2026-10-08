# Stardew Valley Browser Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run the supplied Stardew Valley 1.6.15.24356 single-player game entirely in the browser and verify feature parity before release.

**Architecture:** Recover the supplied managed implementation and preserve gameplay. Use a standalone .NET WebAssembly application with KNI/WebGL as the first platform candidate, browser content/audio adapters, and IndexedDB saves. Serve the release as static files through a private Site.

**Tech Stack:** C#, .NET WebAssembly, KNI, JavaScript browser APIs, Python archive/build tooling, IndexedDB, browser automation.

**Spec:** [Approved design](../specs/2026-10-05-stardew-browser-port-design.md).

## Global Constraints

- Reference version: **1.6.15.24356**; input: `Stardew Valley.zip`.
- Gameplay runs in the browser, with no game server or streaming.
- A static host may serve downloads, but it performs no simulation and stores no player saves.
- Reaching the title screen or shipping a farming demo does not meet the goal.
- Preserve asset identities and game-data values.
- Keep original save serialization and migration behavior.
- Retain local net-field and synchronization primitives when single-player code depends on them.
- Preserve in-game achievements and unlocks.
- No game release before all feature-parity groups pass; report any comparison that cannot be performed.
- Pin framework/tool versions after proving compatibility; start without aggressive trimming.

## Review Focus

- An interrupted or quota-limited save must retain the previous valid save and permit export (Task 5).
- A missing or mismatched asset must stop the affected load with a useful error (Task 3).
- Losing browser focus while a key is held must release inputs and preserve pause behavior (Task 6).
- A desktop save with advanced content must survive import, migration, sleep, reload, and export (Tasks 5 and 7).
- Audio blocked before a user gesture must resume afterward without losing cue mappings (Tasks 3 and 6).

## File Structure

`tools/` owns input preparation, recovery, compatibility reporting, and packaging.
`src/Recovered/` contains generated managed sources; preserve a reproducible
baseline outside normal commits and track targeted changes as patches under
`patches/`. `src/Browser/` owns the WebAssembly host. `src/Platform/` owns browser
adapters and public contracts. `tests/` owns tooling, adapter, and browser checks.
`docs/port/` records compatibility and parity evidence. `dist/` is generated output.

### Task 1: Reproducible archive preparation and source recovery

**Files:** `tools/prepare_port.py`, `tools/recover-source.sh`, `.config/dotnet-tools.json`, `.gitignore`, `tests/tools/test_prepare_port.py`, `docs/port/recovery.md`.

**Interfaces:** `prepare_archive(archive: Path, output: Path) -> dict` returns archive identity and extracted paths; recovery produces `src/Recovered/Game/`, `GameData/`, and supporting managed projects plus a dependency report. The input hash must match `docs/port/archive-inventory.json`.

- [x] Write tests named `test_rejects_path_traversal`, `test_checks_archive_identity`, and `test_extracts_required_assets`; assert no files escape the output directory, wrong archives fail, and game/data assemblies plus content are retained.
- [x] Run `python3 -m unittest discover -s tests/tools -v`; confirm the missing implementation causes failure.
- [x] Implement archive checks, safe extraction, and deterministic reports; ignore generated source baselines, archives, extracted binaries, build output, and local tools.
- [x] Pin ILSpyCmd in the local tool manifest; recover each required managed assembly with project output and archive-local references. Preserve resource files. Record native imports and framework-specific references.
- [x] Restore/build the recovered desktop projects to identify recovery defects independently of browser defects. Store diagnostics and targeted recovery patches; do not treat a failing build as completion.
- [x] Run the tooling tests and repeat recovery into a fresh temporary directory; compare source hashes. Commit reproducible tooling and the evidence report.

### Task 2: Prove the browser graphics and framework boundary

**Files:** `src/Browser/Browser.csproj`, `src/Browser/Program.cs`, `src/Browser/wwwroot/index.html`, `src/Platform/Platform.csproj`, `src/Platform/BrowserGameHost.cs`, `global.json`, `package.json`, `tests/browser/driver.mjs`, `docs/port/compatibility.md`, `tests/browser/compatibility.spec.mjs`.

**Interfaces:** `BrowserGameHost.StartAsync(CancellationToken) -> Task` initializes a canvas host; the browser publishes `window.portStatus = { phase, error }`. Phases are `loading`, `ready`, and `failed`; the test host uses actual archive textures/fonts/maps. `tests/browser/driver.mjs` exports `withGame(testBody: (page: Page) => Promise<void>) -> Promise<void>`, serving published files locally and cleaning up its Playwright browser/server after each test. Browser tests use Node's test runner and assertions; Playwright is a pinned development dependency.

- [x] Write a browser test requiring `ready`, a rendered real texture/font, keyboard and pointer response, successful render-target use, and no uncaught browser exception. Run it against the absent host and confirm failure.
- [x] Inspect the selected KNI release's browser template and pin compatible .NET/KNI versions; create a standalone host with interpretation and trimming disabled initially.
- [x] Implement the host and exercise real content readers, sprite batching, map rendering, render targets, and archive shader effects; report unsupported APIs explicitly.
- [x] Run `dotnet publish src/Browser/Browser.csproj -c Release` and `node --test tests/browser/compatibility.spec.mjs` against its static output; require build success and all assertions passing.
- [x] Resolve framework gaps in `src/Platform/Compatibility/` with narrowly scoped adapters. If this path cannot preserve the contracts, build the compatible-backend fallback described in the spec; record the switch and its evidence.
- [x] Commit the first verified browser rendering milestone and the compatibility report. It is not a game release.

### Task 3: Preserve content and audio behavior

**Files:** `tools/build_content.py`, `src/Platform/Content/BrowserContentStore.cs`, `src/Platform/Audio/BrowserAudioAdapter.cs`, `src/Browser/wwwroot/platform/audio.js`, `tests/tools/test_build_content.py`, `tests/Platform.Tests/ContentTests.cs`, `tests/browser/audio.spec.mjs`.

**Interfaces:** `BrowserContentStore.PreloadAsync(IEnumerable<string>, CancellationToken) -> Task`; `Open(string assetName) -> Stream` reads a verified resident asset. `BrowserAudioAdapter.UnlockAsync() -> Task` enables audio after a gesture. Retain the recovered game's cue interfaces and identifiers.

- [x] Write tests for stable asset-name mapping, checksum mismatch, missing asset errors, compressed-XNB decoding, custom data readers, and xTile external references. Add browser assertions that audio is blocked before a gesture and a known original cue plays after unlock.
- [x] Run `python3 -m unittest discover -s tests/tools -v`, `dotnet test tests/Platform.Tests`, and `node --test tests/browser/audio.spec.mjs`; confirm new expectations fail.
- [x] Generate a versioned manifest with paths, hashes, sizes, and load groups. Preload required synchronous dependencies before initialization or location transitions; preserve content identity.
- [x] Inspect XACT bank codecs and KNI playback support; implement supported playback or a deterministic conversion preserving all cue/category/loop/runtime-variable metadata. Decode effects on demand and stream/cache music within browser limits.
- [x] Run the tests and validate every manifest entry and audio cue mapping against the archive; measure startup bytes and resident audio memory. Commit adapters, tooling, and results.

### Task 4: Integrate the actual recovered game

**Files:** `src/Browser/Browser.csproj`, `src/Platform/BrowserGameHost.cs`, `src/Platform/Services/OfflinePlatformServices.cs`, `patches/game-browser.patch`, `patches/support-browser.patch`, `tools/apply-patches.py`, `tests/browser/new-game.spec.mjs`, `docs/port/compatibility.md`.

**Interfaces:** `BrowserGameHost` initializes the recovered original `Game1` lifecycle. Patches are applied to the reproducible Task 1 baseline before compiling; shared data types and local net-field semantics remain intact.

- [x] Write a browser flow that enters the original title screen, creates a farmer, enters the farm, moves, uses a tool, opens inventory, and sleeps. It must inspect real game state rather than only page text.
- [x] Run `node --test tests/browser/new-game.spec.mjs`; confirm the compatibility host cannot pass it.
- [x] Reference the recovered projects and apply targeted patches for startup, desktop window APIs, native SDK calls, threading, and synchronous file assumptions. Preserve original simulation, events, locations, and menus.
- [x] Adapt threading to cooperative operations or proven browser workers according to each call site's semantics; prevent blocking the browser event loop.
- [x] Run `dotnet publish src/Browser/Browser.csproj -c Release` and `node --test tests/browser/new-game.spec.mjs`; require success and compare representative state transitions with the original implementation. Commit integration and updated compatibility evidence.

### Task 5: Persistent saves, settings, import, and export

**Files:** `src/Platform/Storage/BrowserSaveStore.cs`, `src/Browser/wwwroot/platform/storage.js`, `patches/game-browser.patch`, `tests/Platform.Tests/SaveStoreTests.cs`, `tests/browser/saves.spec.mjs`.

**Interfaces:** `BrowserSaveStore.HydrateAsync() -> Task`; `CommitAsync(string slot, IReadOnlyDictionary<string, byte[]> files) -> Task`; `ImportAsync(IReadOnlyDictionary<string, byte[]> files) -> Task`; `ExportAsync(string slot) -> Task<IReadOnlyDictionary<string, byte[]>>`. Stores original save bytes with per-slot transactional snapshots.

- [x] Write tests for successful reload, interrupted commit, quota failure, original-save import, migration, settings persistence, and export. On failed commits assert the previous snapshot remains byte-identical and exportable.
- [x] Run `dotnet test tests/Platform.Tests` and `node --test tests/browser/saves.spec.mjs`; confirm failure before implementation.
- [x] Implement IndexedDB hydration and atomic commits with a previous-save backup; connect original serializers and migration paths through the virtual filesystem.
- [x] Connect sleep completion to successful persistence; report errors while retaining a valid recoverable save. Implement import/export using file selection and downloads, with validation before replacing a slot.
- [x] Run the tests in a real browser, including a fresh page reload and injected transaction failure. Commit storage and evidence.

### Task 6: Complete browser lifecycle and player controls

**Files:** `src/Browser/wwwroot/platform/lifecycle.js`, `src/Platform/Input/BrowserInputAdapter.cs`, `src/Platform/Services/OfflinePlatformServices.cs`, `src/Browser/wwwroot/index.html`, `tests/browser/lifecycle.spec.mjs`.

**Interfaces:** `BrowserInputAdapter.ReleaseAll()` clears held keys/buttons; the host uses the game's pause behavior on visibility/focus changes. Offline services preserve local achievements, screenshot downloads, and applicable clipboard functionality.

- [x] Write browser assertions for focus loss while holding movement, restored focus, fullscreen/resizing, controller mapping, user-gesture audio restart, and useful asset-loading errors.
- [x] Run `node --test tests/browser/lifecycle.spec.mjs` and confirm missing behaviors fail.
- [x] Implement input release, visibility handling, original pause behavior, fullscreen, and rendering scale; show progress and recoverable errors only outside the original interface.
- [x] Adapt remaining single-player desktop services, including screenshots, achievements, settings, language selection, and clipboard interactions used by the game.
- [x] Verify reload preserves saves/settings and restarting needs no server simulation. Run `node --test tests/browser/lifecycle.spec.mjs`, require all assertions to pass, and commit the completed browser layer.

### Task 7: Verify every single-player feature group

**Files:** `docs/port/feature-parity.md`, `tests/browser/parity.spec.mjs`, `tests/fixtures/scenarios.json`, `src/Platform/Testing/ScenarioBridge.cs` (development builds only), `tests/Platform.Tests/SerializationTests.cs`.

**Interfaces:** `ScenarioBridge.LoadScenarioAsync(string scenarioId) -> Task` loads a reproducibly constructed original save; `Snapshot() -> string` exposes serialized original state to tests. Production builds exclude the bridge. Fixtures record game version and scenario prerequisites.

- [x] Create a parity ledger with every group from the approved spec and status `unverified`; associate each with a concrete new-game or advanced-save scenario and expected outcomes.
- [x] Write scenarios for all original farm layouts; crops/season rollover; inventory/economy; recipes/machines; animals/buildings; fishing; combat/dungeons; NPC/family events; skill/mastery unlocks; both story routes; festivals; island/Qi/perfection; and original minigames.
- [x] Add desktop-save import, migration, full-day/sleep, reload, and export assertions. Use generated fixtures with original serializers or available reference saves; never mark unavailable fixtures verified.
- [x] Run `node --test tests/browser/parity.spec.mjs`, record failures, and fix each adapter or recovery defect without replacing original gameplay logic. Track each targeted fix and rerun its affected checks.
- [x] Verify presentation/audio/localization and compare available reference desktop scenarios. Record unavailable comparisons and browser/performance limits explicitly.
- [x] Run `python3 -m unittest discover -s tests/tools -v`, `dotnet test tests/Platform.Tests`, and `node --test tests/browser/*.spec.mjs` once after the final changes; require all tests and all feature groups to pass. Commit the parity evidence and final fixes.

### Task 8: Build, review, and publish the verified static game

**Files:** `tools/package-browser.sh`, `.github/workflows/deploy-pages.yml`, `README.md`, `docs/port/release-verification.md`.

**Interfaces:** `tools/package-browser.sh` builds verified output into `dist/`; the static host serves this directory without game APIs. The user superseded private Sites hosting with GitHub Pages on 2026-10-08. Actions downloads and verifies the audited release archive, then publishes a Pages artifact.

- [x] Run appropriate source/tooling checks, `dotnet publish src/Browser/Browser.csproj -c Release`, and the complete browser suite against the release output. Inspect for native Windows dependencies and production test hooks.
- [x] Perform an independent final code review using the selected execution workflow; resolve material findings and rerun affected verification.
- [x] Check GitHub Pages package/artifact limits against the actual release: 991,796,702 bytes; preserve original file identities and project-site relative loading paths.
- [x] Upload the checksum-verified production release, publish through GitHub Actions to this repository’s Pages site, follow deployment to a terminal result, and verify the actual origin in Chromium.
- [x] Update README with reproducible commands, controls, save import/export, supported browsers, and the literal deployed URL. Report any verification or deployment limitation; never label an incomplete port complete.

## Execution choice

Native execution is recommended: source recovery and runtime compatibility are
sequential, and their diagnostics determine the exact integration patches.
Implement tasks in this session, then obtain an independent whole-branch review.
Subagent-driven execution remains an option if the user prefers independent
review after each task. Design approval does not select an execution method.
