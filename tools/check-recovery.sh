#!/usr/bin/env bash
set -euo pipefail
port_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$port_root"
python3 -m unittest discover -s tests/tools -v
for project in Game/Stardew-Valley GameData/StardewValley.GameData xTile/xTile BmFont/BmFont CPExtBmFont/CPExtBmFont Lidgren/Lidgren.Network; do
    dotnet build "src/Recovered/$project.csproj" -c Release --nologo
 done
