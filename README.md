# Stardew Valley browser port

Browser-native port of the supplied Windows 1.6.15 archive. The recovered
original C# game runs in WebAssembly with browser graphics, audio and local
IndexedDB saves. A static file host serves the files; gameplay needs no game
server.

GitHub Pages: [https://sharmnten.github.io/Stardew-2/](https://sharmnten.github.io/Stardew-2/). Deployment status is recorded in [release verification](docs/port/release-verification.md).

Development is on `browser-port`, in `.worktrees/browser-port`. All fourteen single-player groups passed the approved representative
acceptance checks on 2026-10-08. See the [parity ledger](docs/port/feature-parity.md) for the fourteen
single-player groups and [release verification](docs/port/release-verification.md)
for production checks and comparison limits.

After [preparing the supplied archive and recovering the source](docs/port/recovery.md),
prepare the original graphics/audio and publish from that checkout:

```sh
tools/recover-graphics.sh
tools/recover-audio.sh
python3 tools/build_audio.py
tools/package-browser.sh
python3 -m http.server 8080 --directory dist
```

Open `http://localhost:8080` in Chromium with WebGL2 and click **Start game**.
The first start downloads and verifies about 64 MB of game content; audio loads
as needed. Use the original title screen to create or load a farm. Default
controls include WASD to move, C/left click to use tools, X/right click to
interact, and Escape for menus. The original Options menu changes bindings.

Saves stay in this browser profile and origin. The **Save files** panel exports
ZIPs and imports desktop save ZIPs or the farm file and `SaveGameInfo` together
at the title screen. Export before clearing browser storage or changing hosts.
The previous committed save remains available if an overnight write fails.
The game package is about 992 MB including lazily loaded lossless audio.

Chromium is the verified browser. Other engines, mobile devices and physical
controllers have not been verified. The port preserves the original gameplay;
acceptance samples all fourteen groups rather than every content permutation.

Run `tools/check-browser.sh` for recovery/tooling, platform, original desktop
reference and Chromium gameplay checks. It publishes development-only scenario
drivers with `BrowserPortTesting=true`. Normal publishes default to `false`;
`tools/check-production-mode.sh` verifies that scenario drivers and fixtures
are excluded. The reference desktop harness runs only during verification.
After packaging, verify ordinary production creation, sleep, save export and
cold Load with:

```sh
PORT_STATIC_ROOT="$PWD/dist" node --test --test-concurrency=1 tests/browser/startup.spec.mjs tests/release/*.spec.mjs
```

Implementation decisions and deferred review observations are recorded in
[acceptance decisions](docs/port/acceptance-decisions.md).

The Pages workflow deploys the checksum-verified production archive attached to
release `browser-1.6.15-2026-10-08`. It uses GitHub Actions; generated game assets
remain outside the source branch. To check the published origin with the same
production flows:

```sh
PORT_STATIC_ROOT="$PWD/dist" PORT_GAME_URL="https://sharmnten.github.io/Stardew-2/" node --test --test-concurrency=1 tests/browser/startup.spec.mjs tests/release/single-player.spec.mjs
```
