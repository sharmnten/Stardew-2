#!/usr/bin/env bash
set -euo pipefail
port_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$port_root"
mkdir -p .port-cache
port_stage=$(mktemp -d "$port_root/.port-cache/release.XXXXXX")
trap 'rm -rf -- "$port_stage"' EXIT
dotnet publish src/Browser/Browser.csproj -c Release -p:BrowserPortTesting=false -p:PublishDir="$port_stage/publish/" --nologo
mkdir -p "$port_stage/audit"
cp src/Browser/bin/Release/net10.0/Browser.dll "$port_stage/audit/Browser.dll"
PORT_BROWSER_ASSEMBLY="$port_stage/audit/Browser.dll" PORT_PRODUCTION_ROOT="$port_stage/publish" node --test tests/reference/build-mode.spec.mjs
python3 - "$port_stage/publish/wwwroot" <<'PY'
import sys
from pathlib import Path
root = Path(sys.argv[1])
for path in root.rglob('*'):
    if path.is_file() and (path.suffix.lower() == '.exe' or path.name.lower() in {
        'steam_api64.dll', 'galaxy64.dll', 'sdl2.dll', 'openal32.dll',
        'hostfxr.dll', 'hostpolicy.dll', 'coreclr.dll', 'steamworks.net.dll', 'galaxycsharp.dll'
    }):
        raise SystemExit('Unexpected desktop dependency: ' + str(path.relative_to(root)))
files = [path for path in root.rglob('*') if path.is_file()]
print(f'Production static game: {len(files)} files, {sum(path.stat().st_size for path in files)} bytes')
PY
# dist contains generated static output only. Prepare it only after the fresh
# publish passes its production and dependency audits.
if [ -e dist ]; then mv dist "$port_stage/previous-dist"; fi
mv "$port_stage/publish/wwwroot" dist
printf 'Static game packaged in %s/dist\n' "$port_root"
