[CmdletBinding()]
param(
    [string]$ProjectPath = (Get-Location).Path,
    [string]$UnityPath,
    [string[]]$AssemblyNames
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $PSCommandPath
$runEditModeTestsPath = Join-Path $scriptDirectory "run-editmode-tests.ps1"
$checkAnalyzersPath = Join-Path $scriptDirectory "check-analyzers.ps1"

if (-not (Test-Path $runEditModeTestsPath)) {
    throw "Missing script: $runEditModeTestsPath"
}

if (-not (Test-Path $checkAnalyzersPath)) {
    throw "Missing script: $checkAnalyzersPath"
}

$testExitCode = 1
$analyzerTotal = -1
$analyzerBlockers = @()

Write-Host "Milestone validation started..."
Write-Host ""
Write-Host "[1/2] Running EditMode tests"

if (-not (Test-Path $ProjectPath)) {
    throw "Project path does not exist: '$ProjectPath'."
}

$testParams = @{
    ProjectPath = $ProjectPath
}

if ($UnityPath) {
    $testParams.UnityPath = $UnityPath
}

if ($AssemblyNames -and $AssemblyNames.Count -gt 0) {
    $testParams.AssemblyNames = $AssemblyNames
}

& $runEditModeTestsPath @testParams
$testExitCode = [int]$LASTEXITCODE

Write-Host ""
Write-Host "[2/2] Running analyzer check"
$analyzerOutput = & $checkAnalyzersPath | ForEach-Object { "$_" }
$analyzerOutput | ForEach-Object { Write-Host $_ }

$totalLine = $analyzerOutput | Where-Object { $_ -match "^TOTAL:\d+$" } | Select-Object -First 1
if ($totalLine -and $totalLine -match "^TOTAL:(\d+)$") {
    $analyzerTotal = [int]$matches[1]
} else {
    $analyzerTotal = -1
}

$analyzerBlockers = @($analyzerOutput | Where-Object { $_ -like "BLOCKER:*" })

$testsPassed = ($testExitCode -eq 0)
$analyzersPassed = ($analyzerTotal -eq 0 -and $analyzerBlockers.Count -eq 0)

$finalExitCode = 0
if (-not $testsPassed -and -not $analyzersPassed) {
    $finalExitCode = 3
} elseif (-not $testsPassed) {
    $finalExitCode = 1
} elseif (-not $analyzersPassed) {
    $finalExitCode = 2
}

Write-Host ""
Write-Host "Milestone Validation Summary"
Write-Host "----------------------------"
if ($testsPassed) {
    Write-Host "Tests: PASS"
} else {
    Write-Host ("Tests: FAIL (exit code {0})" -f $testExitCode)
}

if ($analyzerTotal -ge 0) {
    Write-Host ("Analyzers: {0} (TOTAL:{1}, BLOCKERS:{2})" -f ($(if ($analyzersPassed) { "PASS" } else { "FAIL" }), $analyzerTotal, $analyzerBlockers.Count))
} else {
    Write-Host "Analyzers: FAIL (could not parse TOTAL line)"
}

if ($finalExitCode -eq 0) {
    Write-Host ""
    Write-Host "Quality gates are clean. Milestone is ready to commit."
} else {
    Write-Host ""
    Write-Host "Quality gates failed. Fix issues, then rerun this script."
}

exit $finalExitCode
