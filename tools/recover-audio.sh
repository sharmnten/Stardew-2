#!/usr/bin/env bash
set -euo pipefail
port_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
port_output=${1:-"$port_root/src/Recovered/Xact"}
cd "$port_root"
if [ ! -f original/.archive-manifest.json ]; then
    python3 tools/prepare_port.py
fi
dotnet build tools/Recover/Recover.csproj -c Release
dotnet tools/Recover/bin/Release/net10.0/Recover.dll \
    "$port_root/original/MonoGame.Framework.dll" "$port_output" Microsoft.Xna.Framework.Audio
