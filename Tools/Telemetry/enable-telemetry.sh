#!/usr/bin/env bash
# Launches `claude` with OpenTelemetry enabled, sending data to Honeycomb.
#
# Usage:
#   ./Tools/Telemetry/enable-telemetry.sh           # starts a new claude session
#   ./Tools/Telemetry/enable-telemetry.sh --resume  # forwards args to claude
#
# Requires: a `.env` file in this folder. Copy `.env.example` to `.env` first.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "error: $ENV_FILE not found." >&2
  echo "Copy .env.example to .env and fill in HONEYCOMB_API_KEY." >&2
  exit 1
fi

# Load .env as KEY=VALUE pairs (no shell evaluation, mirrors the .ps1 script).
while IFS= read -r line || [[ -n "$line" ]]; do
  line="${line#"${line%%[![:space:]]*}"}"           # strip leading whitespace
  [[ -z "$line" || "$line" == \#* ]] && continue    # skip blanks and comments
  [[ "$line" != *=* ]] && continue                  # require an '='
  key="${line%%=*}"
  value="${line#*=}"
  key="${key%"${key##*[![:space:]]}"}"              # trim trailing whitespace
  if [[ ${#value} -ge 2 ]]; then                    # strip surrounding quotes
    if [[ "${value:0:1}" == '"' && "${value: -1}" == '"' ]]; then
      value="${value:1:${#value}-2}"
    elif [[ "${value:0:1}" == "'" && "${value: -1}" == "'" ]]; then
      value="${value:1:${#value}-2}"
    fi
  fi
  export "$key=$value"
done < "$ENV_FILE"

if [[ -z "${HONEYCOMB_API_KEY:-}" || "$HONEYCOMB_API_KEY" == "replace-me" ]]; then
  echo "error: HONEYCOMB_API_KEY is not set in $ENV_FILE." >&2
  exit 1
fi

export CLAUDE_CODE_ENABLE_TELEMETRY=1
export OTEL_METRICS_EXPORTER=otlp
export OTEL_LOGS_EXPORTER=otlp
export OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
export OTEL_EXPORTER_OTLP_ENDPOINT="${HONEYCOMB_ENDPOINT:-https://api.honeycomb.io}"
export OTEL_EXPORTER_OTLP_HEADERS="x-honeycomb-team=${HONEYCOMB_API_KEY}"
export OTEL_SERVICE_NAME="${OTEL_SERVICE_NAME:-claude-code}"
export OTEL_METRIC_EXPORT_INTERVAL="${OTEL_METRIC_EXPORT_INTERVAL:-10000}"

if [[ "${LOG_TOOL_DETAILS:-0}" == "1" ]]; then
  export OTEL_LOG_TOOL_DETAILS=1
fi
if [[ "${LOG_USER_PROMPTS:-0}" == "1" ]]; then
  export OTEL_LOG_USER_PROMPTS=1
fi

echo "telemetry: enabled (service=$OTEL_SERVICE_NAME, endpoint=$OTEL_EXPORTER_OTLP_ENDPOINT)"
exec claude "$@"
