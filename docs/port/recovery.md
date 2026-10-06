# Managed source recovery

Implementation is running on branch `browser-port` in
`/workspaces/Stardew-2/.worktrees/browser-port`.

## Reproduce preparation

Place the supplied archive at the checkout root as `Stardew Valley.zip`, then:

```sh
python3 -m unittest discover -s tests/tools -v
python3 tools/prepare_port.py
tools/recover-source.sh
dotnet build 'src/Recovered/Game/Stardew Valley.csproj' -c Release
```

Preparation binds the input to the SHA-256 in `archive-inventory.json`, validates
archive paths, streams extraction into a temporary directory, records per-file
hashes, and refuses to overwrite an existing extraction. The original archive
is unchanged. The extracted `original/` and generated `src/Recovered/` trees
are ignored by Git.

## Evidence so far

- All four extraction tests were observed failing before implementation and
  passing afterward, including identity rejection, traversal rejection,
  asset-byte preservation, and overwrite protection.
- The real archive extracted all 3,817 files with the audited identity.
- ILSpyCmd 11.0.0.9375 recovered the main game, GameData, xTile, BmFont,
  CPExtBmFont, and Lidgren into 1,236 files and 378,330 C# source lines.
- A second independent recovery produced identical hashes for all 1,236 files
  excluding compiler-generated `bin/` and `obj/` directories.
- Recovered GameData, xTile, BmFont, and Lidgren compile against the original
  desktop dependencies. CPExtBmFont requires its missing content-pipeline
  build dependency; its runtime font reader lives in BmFont.
- The initial game compile failed with 18 errors. They involve invalid
  reconstructed primary constructors, omitted local-function bodies,
  an out-of-scope switch variable, and a missing HashFunction.Core reference.
  These are recovery defects, not evidence that the browser port works.

## Recovery configuration investigation

An older ILSpy 9.1 probe also produced the invalid closure reference in
`ForEachItemHelper`; changing only the decompiler version is insufficient.
`tools/Recover/` uses the same pinned ILSpy decompiler library with local-function
and primary-constructor reconstruction disabled. This preserves explicit
compiler-generated closure methods and ordinary constructors. Its output is
being tested before adopting it for the reproducible recovery pipeline.

The recovered `LocalMultiplayer.Initialize` performs dynamic IL generation
during `GameRunner` construction. Browser adaptation must address that startup
path even when only single-player gameplay is requested. Source recovery has
not yet passed the full compilation gate, and no browser gameplay is verified.
