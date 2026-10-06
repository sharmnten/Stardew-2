#!/usr/bin/env bash
set -euo pipefail

port_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
port_output=${1:-"$port_root/src/Recovered"}
cd "$port_root"

if [ ! -f original/.archive-manifest.json ]; then
    python3 tools/prepare_port.py
fi
dotnet tool restore

recover_assembly() {
    local assembly_name=$1
    local project_name=$2
    if [ -e "$port_output/$project_name" ]; then
        printf 'Refusing to replace existing recovered project: %s\n' "$project_name" >&2
        exit 1
    fi
    printf 'Recovering %s\n' "$assembly_name"
    dotnet tool run ilspycmd -- --project --outputdir "$port_output/$project_name" \
        --referencepath "$port_root/original" "$port_root/original/$assembly_name.dll"
}

recover_assembly 'Stardew Valley' Game
recover_assembly StardewValley.GameData GameData
recover_assembly xTile xTile
recover_assembly BmFont BmFont
recover_assembly CPExtBmFont CPExtBmFont
recover_assembly Lidgren.Network Lidgren
