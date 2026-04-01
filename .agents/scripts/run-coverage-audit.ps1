[CmdletBinding()]
param(
    [string]$ProjectPath = (Get-Location).Path,
    [string]$UnityPath,
    [string[]]$AssemblyNames,
    [string]$CoverageResultsPath,
    [string]$CoverageAssemblyFilters,
    [bool]$KeepCoverageArtifacts = $true,
    [int]$CompilationTimeoutMinutes = 10,
    [int]$EditModeTimeoutMinutes = 30,
    [int]$PlayModeTimeoutMinutes = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $PSCommandPath
. (Join-Path $scriptDirectory "..\testing\TestingSuite.Config.ps1")
$checkCompilationPath = Join-Path $scriptDirectory "check-unity-compilation.ps1"
$runEditModeTestsPath = Join-Path $scriptDirectory "run-editmode-tests.ps1"
$runPlayModeTestsPath = Join-Path $scriptDirectory "run-playmode-tests.ps1"

if (-not (Test-Path $checkCompilationPath)) { throw "Missing script: $checkCompilationPath" }
if (-not (Test-Path $runEditModeTestsPath)) { throw "Missing script: $runEditModeTestsPath" }
if (-not (Test-Path $runPlayModeTestsPath)) { throw "Missing script: $runPlayModeTestsPath" }

$compilationExitCode = 1
$editModeExitCode = 1
$playModeExitCode = 1
$coverageExitCode = 0
$coverageSummary = ""

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path
$testingSuiteConfig = Get-TestingSuiteConfig -ProjectPath $resolvedProjectPath
if ([string]::IsNullOrWhiteSpace($CoverageAssemblyFilters)) {
    $CoverageAssemblyFilters = $testingSuiteConfig.CoverageDefaultAssemblyFilters
}
$resolvedCoveragePath = if ([string]::IsNullOrWhiteSpace($CoverageResultsPath)) {
    Join-Path $resolvedProjectPath "Coverage"
} else {
    $CoverageResultsPath
}

Write-Host "Coverage audit started..."
Write-Host ""
Write-Host "[1/4] Running compilation precheck"

$compilationArgs = @{
    ProjectPath = $ProjectPath
    TimeoutMinutes = $CompilationTimeoutMinutes
}
if ($UnityPath) { $compilationArgs.UnityPath = $UnityPath }

try {
    & $checkCompilationPath @compilationArgs
    $compilationExitCode = if (Test-Path variable:LASTEXITCODE) { [int]$LASTEXITCODE } else { 1 }
} catch {
    Write-Host ("Compilation precheck failed before completion: {0}" -f $_.Exception.Message)
    $compilationExitCode = 1
}

if ($compilationExitCode -ne 0) {
    Write-Host ""
    Write-Host "Coverage Audit Summary"
    Write-Host "----------------------"
    Write-Host "Compilation: FAIL"
    Write-Host "Coverage audit blocked because compilation failed."
    exit 1
}

if (Test-Path $resolvedCoveragePath) {
    Remove-Item -Recurse -Force $resolvedCoveragePath
}

Write-Host ""
Write-Host "[2/4] Running EditMode tests with coverage"

$editModeArgs = @{
    ProjectPath = $ProjectPath
    TimeoutMinutes = $EditModeTimeoutMinutes
    EnableCoverage = $true
    CoverageResultsPath = $resolvedCoveragePath
    CoverageOptions = ("assemblyFilters:{0};dontClear" -f $CoverageAssemblyFilters)
}
if ($UnityPath) { $editModeArgs.UnityPath = $UnityPath }
if ($AssemblyNames -and $AssemblyNames.Count -gt 0) { $editModeArgs.AssemblyNames = $AssemblyNames }

try {
    & $runEditModeTestsPath @editModeArgs
    $editModeExitCode = if (Test-Path variable:LASTEXITCODE) { [int]$LASTEXITCODE } else { 1 }
} catch {
    Write-Host ("EditMode test runner failed before completion: {0}" -f $_.Exception.Message)
    $editModeExitCode = 1
}

Write-Host ""
Write-Host "[3/4] Running PlayMode tests with coverage"

$playModeArgs = @{
    ProjectPath = $ProjectPath
    TimeoutMinutes = $PlayModeTimeoutMinutes
    EnableCoverage = $true
    CoverageResultsPath = $resolvedCoveragePath
    CoverageOptions = ("assemblyFilters:{0};dontClear;generateAdditionalReports;generateHtmlReport;generateAdditionalMetrics" -f $CoverageAssemblyFilters)
}
if ($UnityPath) { $playModeArgs.UnityPath = $UnityPath }
if ($AssemblyNames -and $AssemblyNames.Count -gt 0) { $playModeArgs.AssemblyNames = $AssemblyNames }

try {
    & $runPlayModeTestsPath @playModeArgs
    $playModeExitCode = if (Test-Path variable:LASTEXITCODE) { [int]$LASTEXITCODE } else { 1 }
} catch {
    Write-Host ("PlayMode test runner failed before completion: {0}" -f $_.Exception.Message)
    $playModeExitCode = 1
}

Write-Host ""
Write-Host "[4/4] Reading coverage report"

if ($editModeExitCode -eq 0 -and $playModeExitCode -eq 0) {
    $cobertura = Get-ChildItem -Path $resolvedCoveragePath -Recurse -File -Filter *cobertura*.xml -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $cobertura) {
        $coverageExitCode = 1
        $coverageSummary = "Coverage: FAIL (Cobertura report not found under '$resolvedCoveragePath')"
    } else {
        try {
            [xml]$coverageXml = Get-Content -Raw $cobertura.FullName
            $root = $coverageXml.DocumentElement
            if ($null -eq $root) { throw "Cobertura XML has no document root." }

            $lineRateText = $root.GetAttribute("line-rate")
            $branchRateText = $root.GetAttribute("branch-rate")
            if ([string]::IsNullOrWhiteSpace($lineRateText) -or [string]::IsNullOrWhiteSpace($branchRateText)) {
                throw "Cobertura XML is missing line-rate or branch-rate attributes."
            }

            $linePercent = [math]::Round(([double]$lineRateText) * 100.0, 2)
            $branchPercent = [math]::Round(([double]$branchRateText) * 100.0, 2)
            $coverageSummary = "Coverage: PASS (Line: $linePercent%, Branch: $branchPercent%)"
        } catch {
            $coverageExitCode = 1
            $coverageSummary = "Coverage: FAIL (Unable to parse Cobertura report: $($_.Exception.Message))"
        }
    }
} else {
    $coverageExitCode = 1
    $coverageSummary = "Coverage: FAIL (tests failed before coverage parsing)"
}

if (-not $KeepCoverageArtifacts) {
    $rawCoveragePath = Join-Path $resolvedCoveragePath "CleanupProject-opencov"
    if (Test-Path $rawCoveragePath) {
        try { Remove-Item -Recurse -Force $rawCoveragePath } catch { }
    }
}

$testsPassed = ($editModeExitCode -eq 0 -and $playModeExitCode -eq 0)
$finalExitCode = if ($testsPassed -and $coverageExitCode -eq 0) { 0 } else { 1 }

Write-Host ""
Write-Host "Coverage Audit Summary"
Write-Host "----------------------"
Write-Host ("Compilation: {0}" -f ($(if ($compilationExitCode -eq 0) { "PASS" } else { "FAIL" })))
Write-Host ("EditMode:    {0}" -f ($(if ($editModeExitCode -eq 0) { "PASS" } else { "FAIL" })))
Write-Host ("PlayMode:    {0}" -f ($(if ($playModeExitCode -eq 0) { "PASS" } else { "FAIL" })))
Write-Host $coverageSummary
Write-Host ("Coverage path: {0}" -f $resolvedCoveragePath)

exit $finalExitCode
