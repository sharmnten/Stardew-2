# Browser compatibility evidence

## Verified graphics boundary

This is a framework verification host. The recovered Stardew Valley game is
not integrated yet, and single-player feature parity remains unverified.

Pinned environment:

- .NET SDK 10.0.401, browser target net10.0, WebAssembly components 10.0.12.
- KNI framework/platform packages 4.3.9001, browser helpers 10.0.3.
- Playwright 1.63.0 and agent-browser 0.38.2.
- Runtime interpretation; trimming, AOT and development static compression disabled.
- WebGL2/HiDef. Reach rejects the original non-power-of-two compressed font atlas.

The browser reads the actual archive content, including compressed XNBs, the
original xTile map reader, and external farm tile sheets. The xTile browser
project compiles unchanged recovered sources against KNI and retains the
`xTile` assembly identity.

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

Open `http://localhost:4173`. This serves static files only; simulation runs
inside the browser. The current verification page explicitly labels gameplay
integration as pending. The full publish tree includes original XNB assets;
only probe dependencies are downloaded at startup. Audio banks, complete
content verification, storage, lifecycle behavior and the recovered Game1
runtime are addressed by the remaining plan tasks.
