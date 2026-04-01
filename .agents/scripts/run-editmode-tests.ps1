#!/usr/bin/env pwsh
# Forwards to run-unity-tests.ps1 (preserves existing README / doc command lines).
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$target = Join-Path (Split-Path -Parent $PSCommandPath) "run-unity-tests.ps1"
& $target -TestPlatform EditMode @args
