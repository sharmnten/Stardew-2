# Compatibility preflight

This is a metadata inspection of the supplied archive, not an execution test.
No game files were extracted or executed and no product dependencies were
installed. The game and browser runtime remain unimplemented.

[Machine-readable results](compatibility-preflight.json) contain the inspected
assemblies' references, native imports, and relevant platform type references.
[Archive identity](archive-inventory.json) records the input SHA-256.

## Managed implementation size

| Assembly | Type-definition rows | Method-definition rows | Native imports |
| --- | ---: | ---: | ---: |
| Stardew Valley | 1,887 | 18,367 | 9 |
| StardewValley.GameData | 170 | 539 | 0 |
| xTile | 35 | 380 | 0 |
| BmFont | 10 | 96 | 0 |
| CPExtBmFont | 3 | 6 | 0 |
| Lidgren.Network | 71 | 734 | 0 |
| MonoGame.Framework | 1,071 | 7,337 | 7 |
| TextCopy | 26 | 99 | 15 |

These counts include nested and compiler-generated types and methods. They
are not counts of gameplay features or recovered source files.

## Explicit native boundaries in the game assembly

- `liblwjgl_lz4`: three compression/decompression entry points.
- `Imm32.dll`: `ImmGetContext` and `ImmAssociateContext`.
- `user32.dll`: `CallWindowProc` and `SetWindowLong`.
- `SDL2.dll`: `SDL_GetClipboardText` and `SDL_SetClipboardText`.

Source recovery must locate the call sites and determine which run during
single-player play. Absence of a native import in an assembly does not make
its transitive dependencies browser compatible. In particular, the game
references Steam/GOG types and SkiaSharp as well as the listed imports.

## Additional runtime probes required

The game references `Thread`, `Monitor`, `Interlocked`, tasks, filesystem
operations, process APIs, and `System.Reflection.Emit` types including
`DynamicMethod`, `AssemblyBuilder`, and `ILGenerator`. References alone do not
prove that every API is invoked in a single-player path. The recovered call
sites need inspection, followed by tests in the selected browser runtime.

The [.NET browser runtime documentation](https://github.com/dotnet/runtime/blob/main/src/mono/wasm/features.md)
documents experimental threading with required isolation headers, limitations
on blocking the main thread, and streamed HTTP responses that do not support
synchronous reads. Check the pinned release's behavior before relying on
those APIs. Materialize asynchronously downloaded content before passing it
to the game's synchronous readers.

## Content and audio

All 3,550 XNB headers were inspected: 3,541 have platform `w`, version 5,
flags `0x81`; eight have `w`, version 5, flags `0x01`; one has `d`, version 5,
flags `0x00`. Payload decoding and reader compatibility have not been tested.

Both XWB files have `WBND` signatures, version 46, header version 44, flags
524288, and 24-byte entry metadata. The main bank contains 437 wave entries;
the secondary bank contains 26. These are wave entries, not a proven count of
sound cues. Codecs, cue relationships, decoding, and playback remain untested.

## Consequence for implementation

The implementation plan remains appropriate, but source recovery must identify
the dynamic-code and native call paths before claiming browser compatibility.
The first graphics milestone must exercise the actual compressed content,
and audio verification must cover both banks. No feature-parity status changes
follow from this metadata inspection.
