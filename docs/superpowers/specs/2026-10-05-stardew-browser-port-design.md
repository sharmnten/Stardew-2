# Stardew Valley browser-native port: proposed design

Status: approved by the user; implementation-plan review is next. No game implementation exists yet.

## Intended outcome

Convert the supplied `Stardew Valley.zip` into a browser-native game with all
single-player gameplay from the supplied version. The user explicitly requires
that gameplay run in the browser, with no game server or streaming.

The reference version is **1.6.15.24356**, as recorded in the archive's dependency
manifest. Completion means the actual single-player game works, including its
late-game systems; reaching the title screen or shipping a farming demo does not
meet the goal. A static host may serve downloads, but it performs no simulation
and stores no player saves.

## Verified input

The archive passes ZIP CRC validation. It contains 3,817 files, including 3,550
XNB assets, and expands to 691,846,347 bytes. Content totals 561,626,710 bytes;
the largest audio bank alone is 460,453,688 bytes.

The executable targets `.NETCoreApp,Version=v6.0/win-x64`, with the .NET 6.0.32
runtime, MonoGame DesktopGL 3.8.0.1641, xTile, BmFont, SkiaSharp, and platform
SDK libraries. There are no C# source files, solution files, or project files
in the input archive. XML API documentation and game/data assemblies are
present. The machine has .NET SDK 10.0.401, Node, and Python available.

See [the machine-readable archive inventory](../../port/archive-inventory.json)
for the source SHA-256, dependency list, and content groups.

## Approaches considered

1. **Recover and port the C# implementation — recommended.** Recover compilable
   projects from the supplied managed assemblies using ILSpy, retain game logic
   and content, and replace desktop platform boundaries with browser adapters.
   KNI's WebGL backend is the first framework candidate because it derives from
   the XNA/MonoGame family and supports web browsers. This minimizes reimplementation
   of gameplay, but framework compatibility remains unproven.
2. **Build a MonoGame-compatible browser backend.** Retain the original managed
   assembly contracts and implement missing graphics, audio, and platform APIs.
   This is a fallback if KNI incompatibilities require disproportionate changes
   to gameplay. It entails more framework work and cannot be assumed to work
   simply by referencing the Windows DLLs from a WebAssembly app.
3. **Reimplement the game in JavaScript.** A new engine can consume converted
   assets, but every behavior would require reconstruction and parity checking.
   This has the greatest risk of missing single-player features and is not the
   recommended approach for the requested fidelity.

## Architecture

Use a standalone .NET WebAssembly application, initially with interpretation
and without aggressive trimming to preserve reflection and serialization.
Select and pin the SDK and KNI versions together after a compatibility build;
the installed SDK's presence is not evidence that the game supports it.

Keep recovered gameplay in a dedicated project. Keep browser adaptations in a
separate platform layer wherever the original interfaces permit it. Compile
the supplied game-data types and supporting managed libraries against the
selected framework. Record necessary changes to recovered code and their
behavioral implications. Do not use the bundled Windows .NET runtime in the
browser build.

Retain local net-field and synchronization primitives when single-player code
depends on them. Disabling multiplayer does not justify deleting shared game
state infrastructure. Replace platform service startup with an offline
implementation; preserve in-game achievements and unlocks. Native Steam/GOG
account services are platform integrations, and browser save import/export
will replace access to desktop save folders.

### Rendering and input

Adapt the game's XNA graphics and window APIs to WebGL and a canvas, preserving
sprite batching, render targets, effects, pixel scaling, and the original UI.
Route keyboard, mouse, and controller input through the framework. Provide
fullscreen and browser-focus handling. Release held inputs when focus is lost;
pause single-player time according to the original game's pause behavior.

### Content loading

Generate a deterministic, versioned content manifest from the archive. Identify
and support the actual XNB readers, compression formats, xTile map references,
fonts, texture formats, and shader effects before choosing conversions.
Preserve asset identities and game-data values.

Bridge asynchronous HTTP/cache loading to the game's synchronous content APIs
through preloading and an in-memory content store. Load core content before
game initialization; preload required destination assets before location
transitions. Missing content must produce an actionable error, rather than a
silently substituted item, map, or empty event.

### Audio

Inspect both wave banks, the sound bank, and global settings. Verify whether
KNI can reproduce the supplied bank formats in the browser. If conversion is
needed, preserve cue mappings, tracks, looping, pitch, volume, categories,
and runtime variables in a browser audio adapter. Start audio after a user
gesture, as required by browsers. Avoid decoding the entire audio bank into
PCM at startup; load and cache music and effects as needed.

### Saves and settings

Keep original save serialization and migration behavior. Use a virtual file
store backed by IndexedDB for saves and configuration. Hydrate it before save
selection. Commit save data transactionally after sleeping, keep the previous
valid save, and confirm persistence before claiming the save succeeded.
Support desktop save import and export using file selection and downloads.
Quota or persistence failures must retain the last valid save and offer export.

### Browser lifecycle and downloads

Show download progress, a user-gesture start button, and useful load failures.
Once initialized, use the original game interface. Avoid adding menus that
replace existing gameplay. Cache versioned assets without mixing versions.
Make restarting and reopening the game restore saves and settings. Do not
depend on page-unload events for normal saving.

## Implementation sequence

Each milestone is part of the same port; none reduces the feature requirement.

1. Recover the managed source and dependency graph. Establish a reproducible
   compile and document native calls, threading, file access, and framework
   differences. Keep extracted/generated bulk data out of routine source commits.
2. Prove the browser backend with real game content: rendering, input, fonts,
   maps, effects, and representative audio cues. Resolve incompatibilities
   before adapting the whole application.
3. Boot the actual game through character creation into the farm. Verify a
   full day, sleep, persistent save, browser reload, and continuation.
4. Adapt remaining single-player platform boundaries and verify all feature
   groups below using new-game and advanced-save scenarios.
5. Build the static release, verify it in browsers, and publish privately through
   Sites after the complete experience passes verification. Check hosting file
   and package limits before attempting to upload the large content archive.

If source recovery or framework compatibility fails, report the specific
evidence and attempt the compatible-backend fallback. Do not silently replace
the port with a simplified game or publish an unfinished milestone as complete.

## Feature-parity verification

All entries begin unverified. Preserve the original implementation for every
group and exercise representative behavior with new and advanced saves.

| Group | Required checks |
| --- | --- |
| Start and configuration | New/load game, character creation, all original farm layouts, options, controls, localization |
| Farming | Tools, soil, watering, crops, seasons, weather, fruit/wild trees, greenhouse |
| Economy and inventory | Shops, selling/shipping, currencies, item quality, stacking, chests, tool upgrades |
| Crafting and production | Crafting, cooking, machines, automation items, tailoring, furniture and decoration |
| Animals and buildings | Construction, upgrades, renovations, livestock, pets, mounts, production and friendship |
| Fishing and gathering | Fishing minigame, tackle/bait, crab pots, ponds, forage, resource regeneration |
| Exploration and combat | Maps/warps, mines, Skull Cavern, monsters, weapons, buffs, volcano, dungeon generation |
| Characters and family | Schedules, dialogue, gifts, friendship, heart events, marriage, children |
| Progression | Skills, professions, mastery, achievements, collections and unlocks |
| Story and quests | Community Center/Joja routes, mail, quests, special orders, secrets, museum |
| Calendar and events | Festivals, passive festivals, birthdays, random events, cutscenes, movies |
| Late game and minigames | Ginger Island, walnuts, Qi challenges, perfection, original minigames |
| Persistence | Sleep/day rollover, save/reload, settings, import/export, migration, failed-write recovery |
| Presentation | Original UI, fonts, effects, sound/music, resizing/fullscreen, keyboard/mouse/controller |

Automate deterministic tests for save transactions, asset identity/decoding,
serialization, migrations, and platform adapters. Use real browser tests for
rendering, audio, input, reload persistence, and representative gameplay flows.
Compare recovered gameplay behavior with the supplied desktop version where
it can be executed. Record any comparison that cannot be performed.

The release must require no game server, contain no Windows-native runtime
dependency, and show no unexplained browser exceptions in tested flows. Report
browser compatibility and observed performance, rather than inventing an FPS
guarantee or declaring every feature proven from a successful build alone.

## Risks and unresolved compatibility

Source recovery may yield code requiring repairs. KNI may not match the game's
MonoGame extensions. Desktop threading and blocking I/O may require cooperative
browser operations. XACT formats, shaders, and SkiaSharp calls need explicit
compatibility work. Browser memory, storage quotas, and hosting limits may
require segmented assets. These are engineering unknowns to resolve, not
evidence of an already feasible or completed full port.

## Primary technical references

- [KNI browser support and framework lineage](https://github.com/kniEngine/kni)
- [ILSpy managed-code decompiler](https://github.com/icsharpcode/ILSpy)
- [MonoGame DesktopGL platform dependencies](https://docs.monogame.net/articles/getting_started/platforms.html)
- [.NET WebAssembly native dependency requirements](https://learn.microsoft.com/en-us/aspnet/core/blazor/webassembly-native-dependencies?view=aspnetcore-10.0)
- [Standalone Blazor WebAssembly hosting](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/?view=aspnetcore-10.0)
