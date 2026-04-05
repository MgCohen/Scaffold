#!/usr/bin/env pwsh
# One-time / occasional: verify pwsh, print UNITY_PATH hints, optionally emit .agents/local.env from the example.
[CmdletBinding()]
param(
    [switch]$WriteLocalEnvFromExample
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$examplePath = Join-Path $repoRoot ".agents/local.env.example"
$localEnvPath = Join-Path $repoRoot ".agents/local.env"

Write-Host "Scaffold repo: $repoRoot"
Write-Host ("PowerShell version: {0}" -f $PSVersionTable.PSVersion)
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Warning "PowerShell 7+ (pwsh) is recommended for cross-platform scripts."
}

if ($WriteLocalEnvFromExample) {
    if (-not (Test-Path $examplePath)) {
        throw "Missing $examplePath"
    }
    if (Test-Path $localEnvPath) {
        throw "Refusing to overwrite existing $localEnvPath (remove it first or copy manually)."
    }
    Copy-Item -LiteralPath $examplePath -Destination $localEnvPath
    Write-Host "Created $localEnvPath — edit UNITY_PATH, then source or load before running gates."
    exit 0
}

Write-Host ""
Write-Host "Set UNITY_PATH to your Unity executable, for example:"
Write-Host "  Windows: set UNITY_PATH=C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe"
Write-Host "  macOS:   export UNITY_PATH=/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity"
Write-Host ""
Write-Host "Optional: pwsh -File .agents/scripts/setup-environment.ps1 -WriteLocalEnvFromExample"
Write-Host "Docs: .agents/scripts/README.md and Docs/Testing.md"
