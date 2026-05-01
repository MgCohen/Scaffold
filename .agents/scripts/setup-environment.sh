#!/usr/bin/env bash
set -euo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
echo "Scaffold repo: $REPO_ROOT"
if command -v pwsh >/dev/null 2>&1; then
  echo "Found: $(command -v pwsh)"
  pwsh -NoProfile -Command '$PSVersionTable.PSVersion'
else
  echo "Install PowerShell 7+ (pwsh): https://learn.microsoft.com/powershell/scripting/install/installing-powershell"
  echo "  macOS: brew install --cask powershell"
fi
echo ""
echo "Set UNITY_PATH to your Unity binary, e.g.:"
echo "  export UNITY_PATH=\"/Applications/Unity/Hub/Editor/6000.0.1f1/Unity.app/Contents/MacOS/Unity\""
echo ""
EXAMPLE="$REPO_ROOT/.agents/local.env.example"
LOCAL="$REPO_ROOT/.agents/local.env"
if [[ -f "$EXAMPLE" && ! -f "$LOCAL" ]]; then
  echo "Copy optional env file: cp \"$EXAMPLE\" \"$LOCAL\" && edit UNITY_PATH"
fi
echo "See: $REPO_ROOT/.agents/scripts/README.md"
