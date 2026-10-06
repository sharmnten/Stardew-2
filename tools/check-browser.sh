#!/usr/bin/env bash
set -euo pipefail
port_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$port_root"
python3 -m unittest discover -s tests/tools -v
if [ ! -f src/Recovered/Xact/Microsoft.Xna.Framework.Audio/Cue.cs ]; then
    tools/recover-audio.sh
fi
python3 tools/build_audio.py
dotnet test tests/Platform.Tests --nologo
dotnet publish src/Browser/Browser.csproj -c Release --nologo
node --test --test-concurrency=1 tests/browser/*.spec.mjs
