#!/usr/bin/env bash
set -euo pipefail
port_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$port_root"
dotnet build tools/Recover/Recover.csproj -c Release
dotnet tools/Recover/bin/Release/net10.0/Recover.dll \
  "$port_root/original/MonoGame.Framework.dll" "$port_root/src/Recovered/Graphics" Microsoft.Xna.Framework.Graphics
dotnet tools/Recover/bin/Release/net10.0/Recover.dll \
  "$port_root/original/MonoGame.Framework.dll" "$port_root/src/Recovered/Content" Microsoft.Xna.Framework.Content
