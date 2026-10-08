# Browser release verification

Target: the supplied Stardew Valley **1.6.15.24356** archive. The game runs
locally in WebAssembly/WebGL with Web Audio and IndexedDB. Static hosting
serves assets; it does not run game simulation.

Local gameplay and production acceptance are **verified** on 2026-10-08.
The requested deployment target is GitHub Pages at
`https://sharmnten.github.io/Stardew-2/`. Hosting and deployed-origin runtime
acceptance are recorded separately from these local checks; publication is pending. The independent source review
found no critical issue and three important release issues. Their fixes remove
shipping save-failure instrumentation, add a player Start button with verified
content download progress, and expose actionable audio-asset errors while
retaining save/export access. All fixes have observed RED→GREEN evidence and
the complete 66-check browser corpus passed after those review fixes. The
subsequent Pages transport fix is verified separately below; original gameplay
and asset identities are unchanged.

## Completed final checks

| Check | Result |
| --- | --- |
| Python recovery/content/audio tooling | 17/17 passed |
| .NET platform adapters, including transient-host retry boundaries | 47/47 passed |
| .NET game adapters/logger | 4/4 passed |
| Supplied desktop reference and release-host checks | 33/33 passed |
| Original desktop reload of current browser-exported advanced save | 1/1 passed |
| Fresh production hook/dependency audit | 1/1 passed |
| Production Start/progress, injected HTTP 503 recovery, creation/night/export/cold Load | 3/3 passed |
| Complete final browser gameplay suite |66/66 passed (30/30 +36/36; all 33 specs) |

The production runtime checks use `dist/` and have no scenario mutation API or
storage failure-injection API. The reference comparisons and full browser suite
use development fixtures constructed by the original serializer. Both builds
retain the same recovered gameplay implementation.

## Reproducible checks

```sh
tools/check-browser.sh
tools/package-browser.sh
PORT_STATIC_ROOT="$PWD/dist" node --test --test-concurrency=1 tests/browser/startup.spec.mjs tests/release/*.spec.mjs
```

The complete development suite compares the original supplied desktop game
with browser flows across the fourteen groups in the
[parity ledger](feature-parity.md). Development fixtures and mutation drivers
are excluded from normal builds. The production test uses ordinary original
creation, bed/sleep, save export and Load controls. The production audit checks
managed driver exclusion and absence of JavaScript save-failure instrumentation.
Failure injection exists solely in the test harness and aborts real IndexedDB
transactions after writes, proving rollback of both stores.

## Comparison limits

Representative original gameplay and persistence checks meet the approved
acceptance scope; they do not enumerate every crop, item, NPC event, generated
layout or arcade victory path. Browser evidence is Chromium with SwiftShader.
Physical controllers, other browser engines/mobile devices, exact audible
hardware DSP equivalence, real quota exhaustion/eviction/crashes and genuine
older advanced desktop saves remain untested. Constructed original migration
and injected atomic transaction rollback have separate scoped evidence.

No FPS, memory ceiling or startup-time guarantee is implied. The content/audio
inventory records original asset identities; startup downloads are checksum
verified and audio is loaded lazily. Deployed-origin runtime and performance
are not included in the local browser evidence.

## Deferred review observations

Detailed readonly game snapshots and temporary title-click diagnostics remain
on production frames; their performance cost has not been measured. The
literal-filename package audit does not identify fingerprinted dormant managed
Steam/Galaxy wrappers; these are browser assembly containers and do not prove
native libraries are loaded. The historical graphics/development labels have
been replaced as part of release presentation; the ready status now describes browser saves.

## Package and hosting size check

The fresh production package contains **4,279 files / 991,796,702 bytes**.
Its largest file is `Audio/0/091.flac`, **24,267,519 bytes**. These are below
GitHub Pages' documented 1 GB published-site limit. The workflow uses
`actions/upload-pages-artifact` and `actions/deploy-pages`, preserving directories
such as `_framework` and `_content` without Jekyll processing. Relative URLs and
`<base href="./">` retain the `/Stardew-2/` project-site prefix. See
[GitHub Pages limits](https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits)
and [Pages artifact requirements](https://github.com/actions/upload-pages-artifact#artifact-validation).
The first Pages workflow (run `37796122728`) successfully published the original
archive. Separate live checks exposed intermittent Fastly/Varnish HTTP 503
responses for valid content paths. Shared static content/audio/JSON downloads now
retry 408/429/502/503/504 at most four times with cancellation-aware 250/500/1000 ms
backoff. Permanent failures and checksum mismatches still fail visibly. The native
regressions failed before this fix; the complete platform suite now passes 47/47.

The rebuilt package passed all three production checks beneath `/Stardew-2/`
(232,185 ms): startup/progress, one injected 503 followed by verified original
bytes, and actual farm creation/night/save/export/cold Load. Browser assertions
accept a resource warning only when the same URL's latest request finishes
successfully; game logger/page exceptions and unresolved resources remain fatal.

The replacement release `browser-1.6.15-2026-10-08.1` is a 954,674,534-byte gzip
archive; its uncompressed tar is 995,112,960 bytes. SHA-256:
`4a9f92d9d483e7709e88125e5dd32e4cceb7a95ddea294a4910fe69a5e9de182`.
Replacement upload, deployment and live browser acceptance remain pending.

## Final corpus execution

The browser tool command was terminated before its final result. The complete
33-file corpus was rerun against one sealed testing build in two disjoint
alphabetical batches: 30/30 in 2,737,596ms and 36/36 in 2,104,833ms. Both exited 0
with no failures, skips or cancellations. The museum-only harness correction
in the second batch did not change any runtime, fixture, common driver or
first-batch test input. The second batch was rerun completely after that fix.
The corrected ready-tool check passes too, including the full three-night
upgrade/counter collection/cold-load journey.

The final advanced browser export reloads in the supplied original desktop game
without manual repair (1/1, 25,133ms). The final production Start/progress/status
and ordinary farm/sleep/export/cold-load checks pass2/2 in 185,867ms. These tests
use local static serving; they do not establish deployed-origin runtime or
performance measurements.
