#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if command -v pwsh >/dev/null 2>&1; then
  exec pwsh -NoProfile -ExecutionPolicy Bypass -File "$SCRIPT_DIR/run-coverage-audit.ps1" "$@"
fi
echo "PowerShell 7+ (pwsh) is required. See: $SCRIPT_DIR/README.md"
exit 1
