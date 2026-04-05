#!/usr/bin/env pwsh
# Forwards to migration/migrate-scaffold-packages.ps1 (preserves historical path).
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$scriptsRoot = Split-Path -Parent $PSCommandPath
& (Join-Path (Join-Path $scriptsRoot "migration") "migrate-scaffold-packages.ps1") @args
