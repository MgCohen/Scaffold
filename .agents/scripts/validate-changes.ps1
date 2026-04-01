[CmdletBinding()]
param(
    [string]$ProjectPath = (Get-Location).Path,
    [string]$UnityPath,
    [string[]]$AssemblyNames,
    [switch]$SkipTests,
    [int]$CompilationTimeoutMinutes = 10,
    [int]$TestTimeoutMinutes = 0,
    [int]$EditModeTimeoutMinutes = 30,
    [int]$PlayModeTimeoutMinutes = 30,
    [int]$AnalyzerTimeoutMinutes = 10,
    [int]$AnalyzerTestsTimeoutMinutes = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-PowerShellScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath,
        [hashtable]$Parameters
    )

    # Call in-process: Windows PowerShell 5.x Start-Process -ArgumentList breaks paths that contain spaces.
    $boundParams = @{}
    if ($Parameters) {
        foreach ($entry in $Parameters.GetEnumerator()) {
            if ($null -eq $entry.Value) {
                continue
            }

            if ($entry.Value -is [array]) {
                $boundParams[$entry.Key] = $entry.Value
            } else {
                $boundParams[$entry.Key] = $entry.Value
            }
        }
    }

    # Run the script without piping into ForEach-Object first; pipelines can clear $LASTEXITCODE on Windows PowerShell 5.x.
    $rawOutput = & $ScriptPath @boundParams 2>&1
    $exit = if (Test-Path variable:LASTEXITCODE) { [int]$LASTEXITCODE } else { 0 }
    $output = @($rawOutput | ForEach-Object { "$_" })

    return [pscustomobject]@{
        ExitCode = $exit
        Output = $output
    }
}

if ($TestTimeoutMinutes -gt 0) {
    $EditModeTimeoutMinutes = $TestTimeoutMinutes
    $PlayModeTimeoutMinutes = $TestTimeoutMinutes
}

$scriptDirectory = Split-Path -Parent $PSCommandPath
$checkScriptsAsmdefReferencesPath = Join-Path $scriptDirectory "check-scripts-asmdef-references.ps1"
$checkPragmaWarningSuppressionsPath = Join-Path $scriptDirectory "check-pragma-warning-suppressions.ps1"
$checkCompilationPath = Join-Path $scriptDirectory "check-unity-compilation.ps1"
$runEditModeTestsPath = Join-Path $scriptDirectory "run-editmode-tests.ps1"
$runPlayModeTestsPath = Join-Path $scriptDirectory "run-playmode-tests.ps1"
$checkAnalyzersPath = Join-Path $scriptDirectory "check-analyzers.ps1"

if (-not (Test-Path $checkScriptsAsmdefReferencesPath)) { throw "Missing script: $checkScriptsAsmdefReferencesPath" }
if (-not (Test-Path $checkPragmaWarningSuppressionsPath)) { throw "Missing script: $checkPragmaWarningSuppressionsPath" }
if (-not (Test-Path $checkCompilationPath)) { throw "Missing script: $checkCompilationPath" }
if (-not (Test-Path $runEditModeTestsPath)) { throw "Missing script: $runEditModeTestsPath" }
if (-not (Test-Path $runPlayModeTestsPath)) { throw "Missing script: $runPlayModeTestsPath" }
if (-not (Test-Path $checkAnalyzersPath)) { throw "Missing script: $checkAnalyzersPath" }

$asmdefAuditExitCode = 1
$asmdefAuditTotal = -1
$asmdefAuditIssues = @()
$pragmaGateExitCode = 1
$pragmaGateTotal = -1
$pragmaGateIssues = @()
$compilationExitCode = 1
$editModeExitCode = 1
$playModeExitCode = 1
$analyzerTotal = -1
$analyzerBlockers = @()
$analyzerDiagnostics = @()

Write-Host "Change validation started..."
Write-Host ""
Write-Host "[1/6] Running scripts asmdef reference audit"

try {
    $asmdefAuditOutput = & $checkScriptsAsmdefReferencesPath -ProjectPath $ProjectPath | ForEach-Object { "$_" }
    $asmdefAuditExitCode = if (Test-Path variable:LASTEXITCODE) { [int]$LASTEXITCODE } else { 1 }
} catch {
    $asmdefAuditOutput = @(
        "TOTAL:-1",
        ("ISSUE:ScriptFailure|{0}|n/a|{1}" -f $checkScriptsAsmdefReferencesPath, $_.Exception.Message)
    )
    $asmdefAuditExitCode = 1
}

$asmdefAuditOutput |
    Where-Object { $_ -notlike "ISSUE:*" } |
    ForEach-Object { Write-Host $_ }

$asmdefTotalLine = $asmdefAuditOutput | Where-Object { $_ -match "^TOTAL:-?\d+$" } | Select-Object -First 1
if ($asmdefTotalLine -and $asmdefTotalLine -match "^TOTAL:(-?\d+)$") {
    $asmdefAuditTotal = [int]$matches[1]
} else {
    $asmdefAuditTotal = -1
}

$asmdefAuditIssues = @($asmdefAuditOutput | Where-Object { $_ -like "ISSUE:*" })

Write-Host ""
Write-Host "[2/6] Running pragma warning suppression gate"

try {
    $pragmaGateOutput = & $checkPragmaWarningSuppressionsPath -ProjectPath $ProjectPath | ForEach-Object { "$_" }
    $pragmaGateExitCode = if (Test-Path variable:LASTEXITCODE) { [int]$LASTEXITCODE } else { 1 }
} catch {
    $pragmaGateOutput = @(
        "TOTAL:-1",
        ("ISSUE:ScriptFailure|{0}|n/a|{1}" -f $checkPragmaWarningSuppressionsPath, $_.Exception.Message)
    )
    $pragmaGateExitCode = 1
}

$pragmaGateOutput |
    Where-Object { $_ -notlike "ISSUE:*" } |
    ForEach-Object { Write-Host $_ }

$pragmaGateTotalLine = $pragmaGateOutput | Where-Object { $_ -match "^TOTAL:-?\d+$" } | Select-Object -First 1
if ($pragmaGateTotalLine -and $pragmaGateTotalLine -match "^TOTAL:(-?\d+)$") {
    $pragmaGateTotal = [int]$matches[1]
} else {
    $pragmaGateTotal = -1
}

$pragmaGateIssues = @($pragmaGateOutput | Where-Object { $_ -like "ISSUE:*" })

Write-Host ""
Write-Host "[3/6] Running compilation precheck"

$compilationArgs = @{
    ProjectPath = $ProjectPath
    TimeoutMinutes = $CompilationTimeoutMinutes
}
if ($UnityPath) { $compilationArgs.UnityPath = $UnityPath }

try {
    $compilationResult = Invoke-PowerShellScript -ScriptPath $checkCompilationPath -Parameters $compilationArgs
    $compilationOutput = @($compilationResult.Output)
    $compilationExitCode = [int]$compilationResult.ExitCode
    foreach ($line in $compilationOutput) { Write-Host $line }
    if ($compilationOutput | Where-Object { $_ -match "^Status:\s+Blocked\b" }) {
        $compilationExitCode = 1
    }
} catch {
    Write-Host ("Compilation precheck failed before completion: {0}" -f $_.Exception.Message)
    $compilationExitCode = 1
}

Write-Host ""
if ($SkipTests) {
    Write-Host "[4/6] EditMode tests skipped (-SkipTests)"
    $editModeExitCode = 0
    Write-Host ""
    Write-Host "[5/6] PlayMode tests skipped (-SkipTests)"
    $playModeExitCode = 0
} else {
    Write-Host "[4/6] Running EditMode tests"

    $editModeArgs = @{
        ProjectPath = $ProjectPath
        TimeoutMinutes = $EditModeTimeoutMinutes
    }
    if ($UnityPath) { $editModeArgs.UnityPath = $UnityPath }
    if ($AssemblyNames -and $AssemblyNames.Count -gt 0) { $editModeArgs.AssemblyNames = $AssemblyNames }

    if ($compilationExitCode -eq 0) {
        try {
            $editModeResult = Invoke-PowerShellScript -ScriptPath $runEditModeTestsPath -Parameters $editModeArgs
            $editModeOutput = @($editModeResult.Output)
            $editModeExitCode = [int]$editModeResult.ExitCode
            foreach ($line in $editModeOutput) { Write-Host $line }
            if ($editModeOutput | Where-Object { $_ -match "^Status:\s+Blocked\b" }) {
                $editModeExitCode = 1
            } elseif ($editModeOutput | Where-Object { $_ -match "^Failed:\s*[1-9]\d*" }) {
                $editModeExitCode = 2
            }
        } catch {
            Write-Host ("Test runner failed before completion: {0}" -f $_.Exception.Message)
            $editModeExitCode = 1
        }
    } else {
        Write-Host "Skipped EditMode tests because compilation precheck failed."
        $editModeExitCode = 1
    }

    Write-Host ""
    Write-Host "[5/6] Running PlayMode tests"

    $playModeArgs = @{
        ProjectPath = $ProjectPath
        TimeoutMinutes = $PlayModeTimeoutMinutes
    }
    if ($UnityPath) { $playModeArgs.UnityPath = $UnityPath }
    if ($AssemblyNames -and $AssemblyNames.Count -gt 0) { $playModeArgs.AssemblyNames = $AssemblyNames }

    if ($compilationExitCode -eq 0) {
        try {
            $playModeResult = Invoke-PowerShellScript -ScriptPath $runPlayModeTestsPath -Parameters $playModeArgs
            $playModeOutput = @($playModeResult.Output)
            $playModeExitCode = [int]$playModeResult.ExitCode
            foreach ($line in $playModeOutput) { Write-Host $line }
            if ($playModeOutput | Where-Object { $_ -match "^Status:\s+Blocked\b" }) {
                $playModeExitCode = 1
            } elseif ($playModeOutput | Where-Object { $_ -match "^Failed:\s*[1-9]\d*" }) {
                $playModeExitCode = 2
            }
        } catch {
            Write-Host ("Test runner failed before completion: {0}" -f $_.Exception.Message)
            $playModeExitCode = 1
        }
    } else {
        Write-Host "Skipped PlayMode tests because compilation precheck failed."
        $playModeExitCode = 1
    }
}

Write-Host ""
Write-Host "[6/6] Running analyzer check (includes analyzer unit tests)"

try {
    $analyzerOutput = & $checkAnalyzersPath -ProjectPath $ProjectPath -TimeoutMinutes $AnalyzerTimeoutMinutes -AnalyzerTestsTimeoutMinutes $AnalyzerTestsTimeoutMinutes | ForEach-Object { "$_" }
} catch {
    $analyzerOutput = @(
        "TOTAL:-1",
        ("BLOCKER:Analyzer script failed before completion: {0}" -f $_.Exception.Message)
    )
}

$analyzerOutput |
    Where-Object { $_ -notlike "DIAG:*" } |
    ForEach-Object { Write-Host $_ }

$totalLine = $analyzerOutput | Where-Object { $_ -match "^TOTAL:\d+$" } | Select-Object -First 1
if ($totalLine -and $totalLine -match "^TOTAL:(\d+)$") {
    $analyzerTotal = [int]$matches[1]
} else {
    $analyzerTotal = -1
}

$analyzerBlockers = @($analyzerOutput | Where-Object { $_ -like "BLOCKER:*" })
$analyzerDiagnostics = @(
    $analyzerOutput |
        Where-Object { $_ -like "DIAG:*" } |
        ForEach-Object { $_.Substring(5) }
)

$compilationPassed = ($compilationExitCode -eq 0)
$editModePassed = ($compilationPassed -and $editModeExitCode -eq 0)
$playModePassed = ($compilationPassed -and $playModeExitCode -eq 0)
$testsPassed = ($editModePassed -and $playModePassed)
$asmdefAuditPassed = ($asmdefAuditExitCode -eq 0 -and $asmdefAuditTotal -eq 0 -and $asmdefAuditIssues.Count -eq 0)
$pragmaGatePassed = ($pragmaGateExitCode -eq 0 -and $pragmaGateTotal -eq 0 -and $pragmaGateIssues.Count -eq 0)
$validationGatePassed = ($asmdefAuditPassed -and $pragmaGatePassed -and $compilationPassed -and $testsPassed)
$analyzersPassed = ($analyzerTotal -eq 0 -and $analyzerBlockers.Count -eq 0)

$finalExitCode = 0
if (-not $validationGatePassed -and -not $analyzersPassed) {
    $finalExitCode = 3
} elseif (-not $validationGatePassed) {
    $finalExitCode = 1
} elseif (-not $analyzersPassed) {
    $finalExitCode = 2
}

Write-Host ""
Write-Host "Change Validation Summary"
Write-Host "----------------------------"
if ($asmdefAuditTotal -ge 0) {
    Write-Host ("Scripts asmdef audit: {0} (TOTAL:{1})" -f ($(if ($asmdefAuditPassed) { "PASS" } else { "FAIL" }), $asmdefAuditTotal))
} else {
    Write-Host "Scripts asmdef audit: FAIL (could not parse TOTAL line)"
}
if ($pragmaGateTotal -ge 0) {
    Write-Host ("Pragma suppression gate: {0} (TOTAL:{1})" -f ($(if ($pragmaGatePassed) { "PASS" } else { "FAIL" }), $pragmaGateTotal))
} else {
    Write-Host "Pragma suppression gate: FAIL (could not parse TOTAL line)"
}
Write-Host ("Compilation: {0} (exit code {1})" -f ($(if ($compilationPassed) { "PASS" } else { "FAIL" }), $compilationExitCode))
if ($testsPassed) {
    Write-Host "Tests: PASS"
} else {
    if ($compilationPassed) {
        Write-Host ("Tests: FAIL (EditMode exit code {0}, PlayMode exit code {1})" -f $editModeExitCode, $playModeExitCode)
    } else {
        Write-Host "Tests: SKIPPED (compilation failed)"
    }
}

if ($analyzerTotal -ge 0) {
    Write-Host ("Analyzers: {0} (TOTAL:{1}, BLOCKERS:{2})" -f ($(if ($analyzersPassed) { "PASS" } else { "FAIL" }), $analyzerTotal, $analyzerBlockers.Count))
} else {
    Write-Host "Analyzers: FAIL (could not parse TOTAL line)"
}

if ($finalExitCode -eq 0) {
    Write-Host ""
    Write-Host "Quality gates are clean. Changes are ready to commit."
} else {
    Write-Host ""
    Write-Host "Quality gates failed. Fix issues, then rerun this script."
}

if (-not $analyzersPassed) {
    Write-Host ""
    Write-Host "AGENT_TASK_BEGIN"
    Write-Host "Task: Fix all analyzer diagnostics and blockers listed below."
    Write-Host "Validation: Re-run .agents/scripts/validate-changes.ps1 and ensure TOTAL:0 with no BLOCKER lines."
    Write-Host "AGENT_TASK_END"
    Write-Host ""
    Write-Host "AGENT_ANALYZER_DIAGNOSTICS_BEGIN"
    foreach ($line in $analyzerBlockers) { Write-Host $line }
    foreach ($line in $analyzerDiagnostics) { Write-Host $line }
    Write-Host "AGENT_ANALYZER_DIAGNOSTICS_END"
}

if (-not $asmdefAuditPassed) {
    Write-Host ""
    Write-Host "AGENT_ASMDEF_AUDIT_BEGIN"
    foreach ($line in $asmdefAuditIssues) { Write-Host $line }
    Write-Host "AGENT_ASMDEF_AUDIT_END"
}

if (-not $pragmaGatePassed) {
    Write-Host ""
    Write-Host "AGENT_PRAGMA_SUPPRESSION_GATE_BEGIN"
    foreach ($line in $pragmaGateIssues) { Write-Host $line }
    Write-Host "AGENT_PRAGMA_SUPPRESSION_GATE_END"
}

exit $finalExitCode
