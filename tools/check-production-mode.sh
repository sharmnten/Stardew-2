#!/usr/bin/env bash
set -euo pipefail
port_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$port_root"
port_audit_root="$port_root/.port-cache/production-compile"
dotnet publish src/Browser/Browser.csproj -c Release --no-restore -p:BrowserPortTesting=false -p:PublishDir="$port_audit_root/" --verbosity quiet
mkdir -p "$port_audit_root/audit"
cp src/Browser/bin/Release/net10.0/Browser.dll "$port_audit_root/audit/Browser.dll"
PORT_BROWSER_ASSEMBLY="$port_audit_root/audit/Browser.dll" PORT_PRODUCTION_ROOT="$port_audit_root" node --test tests/reference/build-mode.spec.mjs
