#!/usr/bin/env bash
set -euo pipefail
port_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$port_root"
python3 -m unittest discover -s tests/tools -v
dotnet test tests/Platform.Tests --nologo
dotnet publish src/Browser/Browser.csproj -c Release --nologo
node --test tests/browser/*.spec.mjs
