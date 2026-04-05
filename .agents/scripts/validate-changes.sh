#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if command -v pwsh >/dev/null 2>&1; then
  exec pwsh -NoProfile -ExecutionPolicy Bypass -File "$SCRIPT_DIR/validate-changes.ps1" "$@"
fi
echo "PowerShell 7+ (pwsh) is required. Install: https://learn.microsoft.com/powershell/scripting/install/installing-powershell"
echo "See: $SCRIPT_DIR/README.md"
exit 1
