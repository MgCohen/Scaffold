#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [ValidateSet("EditMode", "PlayMode")]
    [Parameter(Mandatory = $true)]
    [string]$TestPlatform,
    [string]$ProjectPath = (Get-Location).Path,
    [string]$UnityPath,
    [string[]]$AssemblyNames,
    [string]$PerformanceTestResultsPath,
    [switch]$EnableCoverage,
    [string]$CoverageResultsPath,
    [string]$CoverageOptions,
    [int]$TimeoutMinutes = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $PSCommandPath
. (Join-Path $scriptDir (Join-Path "lib" "UnityProcess.ps1"))
. (Join-Path $scriptDir (Join-Path "lib" "UnityEditorPaths.ps1"))
. (Join-Path (Split-Path $scriptDir -Parent) (Join-Path "testing" "TestingSuite.Config.ps1"))

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path
$testingSuiteConfig = Get-TestingSuiteConfig -ProjectPath $resolvedProjectPath
$resolvedCoveragePath = $null
$tempPrefix = if ($TestPlatform -eq "EditMode") { "unity-editmode-tests-" } else { "unity-playmode-tests-" }
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ($tempPrefix + [guid]::NewGuid().ToString("N"))
$null = New-Item -ItemType Directory -Path $tempRoot -Force
$logName = if ($TestPlatform -eq "EditMode") { "editmode.log" } else { "playmode.log" }
$resultsName = if ($TestPlatform -eq "EditMode") { "editmode-results.xml" } else { "playmode-results.xml" }
$logPath = Join-Path $tempRoot $logName
$resultsPath = Join-Path $tempRoot $resultsName
$scriptExitCode = 1
$resolvedPerfPath = $null

try {
    if ($TimeoutMinutes -lt 1) {
        throw "TimeoutMinutes must be 1 or greater."
    }

    if ($EnableCoverage) {
        $resolvedCoveragePath = if ([string]::IsNullOrWhiteSpace($CoverageResultsPath)) {
            Join-Path $resolvedProjectPath "Coverage"
        } else {
            $CoverageResultsPath
        }

        $null = New-Item -ItemType Directory -Path $resolvedCoveragePath -Force
    }

    $resolvedUnityPath = Resolve-UnityEditorPath -ResolvedProjectPath $resolvedProjectPath `
        -RequestedUnityPath $UnityPath `
        -AllowHubVersionFallback $true `
        -ExampleScriptHint ".agents/scripts/run-unity-tests.ps1"

    if (-not [string]::IsNullOrWhiteSpace($PerformanceTestResultsPath)) {
        $candidatePath = if ([System.IO.Path]::IsPathRooted($PerformanceTestResultsPath)) {
            $PerformanceTestResultsPath
        } else {
            Join-Path $resolvedProjectPath $PerformanceTestResultsPath
        }

        $resolvedPerfPath = [System.IO.Path]::GetFullPath($candidatePath)
        $perfDir = Split-Path -Parent $resolvedPerfPath
        if (-not [string]::IsNullOrWhiteSpace($perfDir) -and -not (Test-Path -LiteralPath $perfDir)) {
            $null = New-Item -ItemType Directory -Path $perfDir -Force
        }
    }

    $unityArgs = @(
        "-batchmode"
        "-accept-apiupdate"
        "-projectPath", $resolvedProjectPath
        "-runTests"
        "-testPlatform", $TestPlatform
        "-testResults", $resultsPath
        "-logFile", $logPath
    )

    if ($resolvedPerfPath) {
        $unityArgs += @("-perfTestResults", $resolvedPerfPath)
    }

    if ($EnableCoverage) {
        $resolvedCoverageOptions = if ([string]::IsNullOrWhiteSpace($CoverageOptions)) {
            "assemblyFilters:$($testingSuiteConfig.CoverageDefaultAssemblyFilters)"
        } else {
            $CoverageOptions
        }

        $unityArgs += @(
            "-enableCodeCoverage"
            "-debugCodeOptimization"
            "-coverageResultsPath", $resolvedCoveragePath
            "-coverageOptions", $resolvedCoverageOptions
        )
    }

    if ($AssemblyNames -and $AssemblyNames.Count -gt 0) {
        $unityArgs += @("-assemblyNames", ($AssemblyNames -join ";"))
    }

    Write-Host ("Running Unity {0} tests..." -f $TestPlatform)
    Write-Host "Project: $resolvedProjectPath"
    Write-Host "Unity:   $resolvedUnityPath"

    $unityProcess = Start-UnityEditorProcess -FilePath $resolvedUnityPath -Arguments $unityArgs
    $timeoutMilliseconds = $TimeoutMinutes * 60 * 1000
    $didExit = $unityProcess.WaitForExit($timeoutMilliseconds)

    if (-not $didExit) {
        try {
            Stop-Process -Id $unityProcess.Id -Force -ErrorAction SilentlyContinue
        } catch {
        }

        $logTail = if (Test-Path $logPath) {
            (Get-Content $logPath -Tail 40) -join [Environment]::NewLine
        } else {
            "No Unity log was written."
        }

        Write-Host ""
        Write-Host "Test Report"
        Write-Host "-----------"
        Write-Host "Status:  Blocked"
        Write-Host "Passed:  0"
        Write-Host "Failed:  0"
        Write-Host "Skipped: 0"
        Write-Host ""
        Write-Host "Details"
        Write-Host "-------"
        Write-Host ("Unity test run timed out after {0} minute(s)." -f $TimeoutMinutes)
        Write-Host $logTail
        if ($resolvedPerfPath) {
            Write-Host ""
            Write-Host ("Performance JSON path (may be missing after timeout): {0}" -f $resolvedPerfPath)
        }
        $scriptExitCode = 1
        return
    }

    $unityExitCode = [int]$unityProcess.ExitCode

    if ($resolvedPerfPath) {
        Write-Host ""
        if (Test-Path -LiteralPath $resolvedPerfPath) {
            Write-Host ("Performance JSON: {0}" -f $resolvedPerfPath)
        } else {
            Write-Host ("Warning: performance JSON was not created at {0}" -f $resolvedPerfPath)
        }
    }

    if (-not (Test-Path $resultsPath)) {
        $logTail = if (Test-Path $logPath) {
            (Get-Content $logPath -Tail 40) -join [Environment]::NewLine
        } else {
            "No Unity log was written."
        }

        Write-Host ""
        Write-Host "Test Report"
        Write-Host "-----------"
        Write-Host "Status:  Blocked"
        Write-Host "Passed:  0"
        Write-Host "Failed:  0"
        Write-Host "Skipped: 0"
        Write-Host ""
        Write-Host "Details"
        Write-Host "-------"
        Write-Host ("Unity exited with code {0} before producing test results." -f $unityExitCode)
        Write-Host $logTail
        if ($resolvedPerfPath) {
            Write-Host ""
            Write-Host ("Performance JSON path (may be missing): {0}" -f $resolvedPerfPath)
        }
        $scriptExitCode = 1
        return
    }

    [xml]$resultsXml = Get-Content -Raw $resultsPath
    $testRun = $resultsXml.'test-run'
    if (-not $testRun) {
        throw "The results XML does not contain a test-run node."
    }

    $failedNodes = @(Select-Xml -Xml $resultsXml -XPath "//test-case[@result='Failed']" | ForEach-Object { $_.Node })
    $passedCount = [int]$testRun.passed
    $failedCount = [int]$testRun.failed
    $skippedCount = [int]$testRun.skipped
    $totalCount = [int]$testRun.total

    Write-Host ""
    Write-Host "Test Report"
    Write-Host "-----------"
    Write-Host ("Total:   {0}" -f $totalCount)
    Write-Host ("Passed:  {0}" -f $passedCount)
    Write-Host ("Failed:  {0}" -f $failedCount)
    Write-Host ("Skipped: {0}" -f $skippedCount)

    if ($failedNodes.Count -gt 0) {
        Write-Host ""
        Write-Host "Failed Tests"
        Write-Host "------------"

        foreach ($failedNode in $failedNodes) {
            $name = $failedNode.fullname
            $message = Get-UnityTestFailureMessage -Node $failedNode
            Write-Host ("- {0}" -f $name)
            if ($message) {
                Write-Host ("  {0}" -f $message)
            }
        }

        $scriptExitCode = 2
        return
    }

    $scriptExitCode = 0
}
catch {
    Write-Host ""
    Write-Host "Test Report"
    Write-Host "-----------"
    Write-Host "Status:  Blocked"
    Write-Host "Passed:  0"
    Write-Host "Failed:  0"
    Write-Host "Skipped: 0"
    Write-Host ""
    Write-Host "Details"
    Write-Host "-------"
    Write-Host $_.Exception.Message
    $scriptExitCode = 1
}
finally {
    if (Test-Path $tempRoot) {
        try {
            [System.IO.Directory]::Delete($tempRoot, $true)
        } catch {
            Start-Sleep -Milliseconds 250
            try {
                [System.IO.Directory]::Delete($tempRoot, $true)
            } catch {
            }
        }
    }
}

exit $scriptExitCode
