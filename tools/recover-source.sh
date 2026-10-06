#!/usr/bin/env bash
set -euo pipefail

port_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
port_output=${1:-"$port_root/src/Recovered"}
cd "$port_root"

if [ ! -f original/.archive-manifest.json ]; then
    python3 tools/prepare_port.py
fi
# Reject the entire operation before generating any project.
for project_name in Game GameData xTile BmFont CPExtBmFont Lidgren; do
    if [ -e "$port_output/$project_name" ]; then
        printf 'Refusing to replace existing recovered project: %s\n' "$project_name" >&2
        exit 1
    fi
done
dotnet tool restore
dotnet build tools/Recover/Recover.csproj -c Release

dotnet tools/Recover/bin/Release/net10.0/Recover.dll \
    "$port_root/original/Stardew Valley.dll" "$port_output/Game"
patch --batch --forward --fuzz=0 -p1 -d "$port_output/Game" < patches/game-recovery.patch

recover_assembly() {
    local assembly_name=$1
    local project_name=$2
    printf 'Recovering %s\n' "$assembly_name"
    dotnet tool run ilspycmd -- --project --outputdir "$port_output/$project_name" \
        --referencepath "$port_root/original" "$port_root/original/$assembly_name.dll"
}

recover_assembly StardewValley.GameData GameData
recover_assembly xTile xTile
recover_assembly BmFont BmFont
recover_assembly CPExtBmFont CPExtBmFont
patch --batch --forward --fuzz=0 -p1 -d "$port_output/CPExtBmFont" < patches/font-pipeline.patch
recover_assembly Lidgren.Network Lidgren
