#!/usr/bin/env bash
# Requires Unity Editor on PATH as `unity-editor` or set UNITY_EDITOR env to the Unity binary.
# Example: UNITY_EDITOR="/path/to/Unity" ./Tools/GraphFlowM0BuildPlayer.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY_EDITOR:-unity-editor}"
"$UNITY" -batchmode -quit -projectPath "$ROOT" -executeMethod Scaffold.GraphFlow.M0.Editor.GraphFlowM0PlayerSmokeBuild.BuildLinux64 -logFile -
