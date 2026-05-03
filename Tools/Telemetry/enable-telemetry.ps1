# Launches `claude` with OpenTelemetry enabled, sending data to Honeycomb.
#
# Usage:
#   pwsh -NoProfile -File .\Tools\Telemetry\enable-telemetry.ps1
#   pwsh -NoProfile -File .\Tools\Telemetry\enable-telemetry.ps1 --resume
#
# Requires: a `.env` file in this folder. Copy `.env.example` to `.env` first.

param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ClaudeArgs
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$envFile   = Join-Path $scriptDir '.env'

if (-not (Test-Path $envFile)) {
    Write-Error "$envFile not found. Copy .env.example to .env and fill in HONEYCOMB_API_KEY."
}

# Parse .env into a hashtable (KEY=VALUE, ignores blanks and comments).
$envVars = @{}
foreach ($line in Get-Content $envFile) {
    $trimmed = $line.Trim()
    if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }
    $idx = $trimmed.IndexOf('=')
    if ($idx -lt 1) { continue }
    $key   = $trimmed.Substring(0, $idx).Trim()
    $value = $trimmed.Substring($idx + 1).Trim().Trim('"').Trim("'")
    $envVars[$key] = $value
}

$apiKey = $envVars['HONEYCOMB_API_KEY']
if (-not $apiKey -or $apiKey -eq 'replace-me') {
    Write-Error "HONEYCOMB_API_KEY is not set in $envFile."
}

$endpoint       = if ($envVars['HONEYCOMB_ENDPOINT']) { $envVars['HONEYCOMB_ENDPOINT'] } else { 'https://api.honeycomb.io' }
$serviceName    = if ($envVars['OTEL_SERVICE_NAME']) { $envVars['OTEL_SERVICE_NAME'] } else { 'claude-code' }
$exportInterval = if ($envVars['OTEL_METRIC_EXPORT_INTERVAL']) { $envVars['OTEL_METRIC_EXPORT_INTERVAL'] } else { '10000' }

$env:CLAUDE_CODE_ENABLE_TELEMETRY   = '1'
$env:OTEL_METRICS_EXPORTER          = 'otlp'
$env:OTEL_LOGS_EXPORTER             = 'otlp'
$env:OTEL_EXPORTER_OTLP_PROTOCOL    = 'http/protobuf'
$env:OTEL_EXPORTER_OTLP_ENDPOINT    = $endpoint
$env:OTEL_EXPORTER_OTLP_HEADERS     = "x-honeycomb-team=$apiKey"
$env:OTEL_SERVICE_NAME              = $serviceName
$env:OTEL_METRIC_EXPORT_INTERVAL    = $exportInterval

if ($envVars['LOG_TOOL_DETAILS'] -eq '1') { $env:OTEL_LOG_TOOL_DETAILS = '1' }
if ($envVars['LOG_USER_PROMPTS'] -eq '1') { $env:OTEL_LOG_USER_PROMPTS = '1' }

Write-Host "telemetry: enabled (service=$serviceName, endpoint=$endpoint)"

if ($ClaudeArgs) {
    & claude @ClaudeArgs
} else {
    & claude
}
