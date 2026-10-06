#!/usr/bin/env bash
set -euo pipefail
port_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$port_root"
python3 -m unittest discover -s tests/tools -v
if [ ! -f src/Recovered/Xact/Microsoft.Xna.Framework.Audio/Cue.cs ]; then
    tools/recover-audio.sh
fi
if [ ! -f src/Recovered/Content/Microsoft.Xna.Framework.Content/Texture2DReader.cs ]; then
    tools/recover-graphics.sh
fi
python3 tools/build_audio.py
dotnet test tests/Platform.Tests --nologo
dotnet test tests/Game.Tests --nologo
node --test --test-concurrency=1 tests/reference/desktop.spec.mjs tests/reference/farming.spec.mjs tests/reference/production-economy.spec.mjs tests/reference/advanced-roundtrip.spec.mjs
dotnet publish src/Browser/Browser.csproj -c Release -p:BrowserPortTesting=true --nologo
node --test --test-concurrency=1 tests/browser/*.spec.mjs
node --test tests/reference/browser-export.spec.mjs
