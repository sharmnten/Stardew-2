# Stardew Valley browser port

Browser-native port of the supplied Windows 1.6.15 archive. The recovered
original C# game runs in WebAssembly with browser graphics, audio and local
IndexedDB saves. A static file host serves the files; gameplay needs no game
server.

Development is on `browser-port`, in `.worktrees/browser-port`. The original
runtime, creation, sleep/save/reload and browser platform adapters are running.
Desktop/browser comparisons also cover farming, economy, recipes, hopper
automation, tailoring, furniture, livestock/buildings, and sign editing saves.
Feature parity is still being verified; this is not the final release. See the
[parity ledger](docs/port/feature-parity.md) for checks and remaining work.

After [preparing the supplied archive and recovering the source](docs/port/recovery.md),
prepare the original graphics/audio and publish from that checkout:

```sh
tools/recover-graphics.sh
tools/recover-audio.sh
python3 tools/build_audio.py
dotnet publish src/Browser/Browser.csproj -c Release
python3 -m http.server 8080 --directory src/Browser/bin/Release/net10.0/publish/wwwroot
```

Open `http://localhost:8080`. Use the original title screen to create or load a
farm. Saves stay in this browser profile. The **Save files** panel exports ZIPs
and imports desktop save ZIPs or the farm file and `SaveGameInfo` together at
the title screen.

Run `tools/check-browser.sh` for recovery/tooling, platform, original desktop
reference and Chromium gameplay checks. It publishes development-only scenario
drivers with `BrowserPortTesting=true`. Normal publishes default to `false`;
`tools/check-production-mode.sh` verifies that scenario drivers and fixtures
are excluded. The reference desktop harness runs only during verification.
