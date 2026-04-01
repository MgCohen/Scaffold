#!/usr/bin/env pwsh
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$scriptsRoot = Split-Path -Parent $PSCommandPath
& (Join-Path (Join-Path $scriptsRoot "migration") "rewrite-docs-package-paths.ps1") @args
