# Managed source recovery

Implementation is running on branch `browser-port` in
`/workspaces/Stardew-2/.worktrees/browser-port`.

## Reproduce preparation

Install the pinned .NET SDK 10.0.401 and Python 3, then place the supplied archive at the checkout root as `Stardew Valley.zip`, then:

```sh
python3 -m unittest discover -s tests/tools -v
python3 tools/prepare_port.py
tools/recover-source.sh
tools/check-recovery.sh
```

Preparation binds the input to the SHA-256 in `archive-inventory.json`, validates
archive paths, streams extraction into a temporary directory, records per-file
hashes, and refuses to overwrite an existing extraction. The original archive
is unchanged. The extracted `original/` and generated `src/Recovered/` trees
are ignored by Git.

## Verified recovery evidence

- All four extraction tests were observed failing before implementation and
  passing afterward, including identity rejection, traversal rejection,
  asset-byte preservation, and overwrite protection.
- The real archive extracted all 3,817 files with the audited identity.
- ILSpyCmd 11.0.0.9375 recovered the main game, GameData, xTile, BmFont,
  CPExtBmFont, and Lidgren into 1,236 files and 378,330 C# source lines.
- A second independent recovery produced identical hashes for all 1,236 files
  excluding compiler-generated `bin/` and `obj/` directories.
- The recovered game and all five supporting projects compile against the
  original desktop dependencies. The game compile reports 82 warnings and zero
  errors; these include original obsolete APIs and recovery warnings.
- `patches/font-pipeline.patch` restores the original content-pipeline build
  dependency, MonoGame.Framework.Content.Pipeline 3.8.0.1641. Its runtime font
  reader lives in BmFont.
- The initial game compile failed with 18 errors. They involve invalid
  reconstructed primary constructors, omitted local-function bodies,
  an out-of-scope switch variable, and a missing HashFunction.Core reference.
  These were recovery defects; the raw output remains a diagnostic baseline.

- The final patched pipeline was run from start to finish into a fresh tree.
  All 1,239 generated files are byte-identical to the working baseline, including
  resources and project references (391,098 C# lines).
  See `recovery-verification.json` for the aggregate source-manifest identity.

## Recovery configuration investigation

An older ILSpy 9.1 probe also produced the invalid closure reference in
`ForEachItemHelper`; changing only the decompiler version is insufficient.
`tools/Recover/` uses ICSharpCode.Decompiler 11.0.0.9375 with local functions,
static local functions, anonymous methods/types, and primary-constructor
reconstruction disabled. C# 11 supports recovered multiline interpolation.
Disabling these transforms together preserves explicit compiler-generated
closure methods and ordinary constructors. The pinned CLI recovers support
projects, which compile with its standard transforms.

`patches/game-recovery.patch` makes five narrow repairs required by the native
compiler: add the archive HashFunction.Core reference, remove an explicit
object constructor call, restore a base-constructor initializer, restore
closure-method accessibility, and move a switch label into the variable
scope it requires. It retains original method bodies and data values.
Recovery applies patches with zero fuzz and refuses existing output projects.
The API names the game project `Game/Stardew-Valley.csproj`; its assembly
identity remains `Stardew Valley`.

The recovered `LocalMultiplayer.Initialize` performs dynamic IL generation
during `GameRunner` construction. Browser adaptation must address that startup
path even when only single-player gameplay is requested. Native source compilation passes; no browser gameplay is verified yet.
