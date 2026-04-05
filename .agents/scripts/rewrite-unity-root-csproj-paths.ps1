#!/usr/bin/env pwsh
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$scriptsRoot = Split-Path -Parent $PSCommandPath
& (Join-Path (Join-Path $scriptsRoot "migration") "rewrite-unity-root-csproj-paths.ps1") @args
