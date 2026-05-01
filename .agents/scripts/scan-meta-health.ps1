#!/usr/bin/env pwsh
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$scriptsRoot = Split-Path -Parent $PSCommandPath
& (Join-Path (Join-Path $scriptsRoot "diagnostics") "scan-meta-health.ps1") @args
