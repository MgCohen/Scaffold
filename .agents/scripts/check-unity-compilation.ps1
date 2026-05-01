#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$ProjectPath = (Get-Location).Path,
    [string]$UnityPath,
    [int]$TimeoutMinutes = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $PSCommandPath
. (Join-Path $scriptDir (Join-Path "lib" "UnityProcess.ps1"))
. (Join-Path $scriptDir (Join-Path "lib" "UnityEditorPaths.ps1"))

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path
$resolvedUnityPath = $null
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("unity-compile-check-" + [guid]::NewGuid().ToString("N"))
$null = New-Item -ItemType Directory -Path $tempRoot -Force
$logPath = Join-Path $tempRoot "compile.log"
$scriptExitCode = 1

try {
    if ($TimeoutMinutes -lt 1) {
        throw "TimeoutMinutes must be 1 or greater."
    }

    $resolvedUnityPath = Resolve-UnityEditorPath -ResolvedProjectPath $resolvedProjectPath `
        -RequestedUnityPath $UnityPath `
        -Strict `
        -ExampleScriptHint ".agents/scripts/check-unity-compilation.ps1"

    $unityArgs = @(
        "-batchmode"
        "-accept-apiupdate"
        "-projectPath", $resolvedProjectPath
        "-quit"
        "-logFile", $logPath
    )

    Write-Host "Running Unity compilation precheck..."
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

        Write-Host ""
        Write-Host "Compilation Check Report"
        Write-Host "------------------------"
        Write-Host "Status:  Blocked"
        Write-Host ""
        Write-Host "Details"
        Write-Host "-------"
        Write-Host ("Unity compilation precheck timed out after {0} minute(s)." -f $TimeoutMinutes)
        if (Test-Path $logPath) {
            Write-Host ((Get-Content $logPath -Tail 40) -join [Environment]::NewLine)
        }
        $scriptExitCode = 1
        return
    }

    $unityExitCode = [int]$unityProcess.ExitCode
    $logLines = @()
    if (Test-Path $logPath) {
        $logLines = @(Get-Content $logPath)
    }

    $compilerErrors = @($logLines | Where-Object {
        $_ -match 'error CS\d+:' -or
        $_ -match '^## Script Compilation Error' -or
        $_ -match '^Scripts have compiler errors\.$'
    } | Select-Object -Unique)

    $projectLockDetected = @($logLines | Where-Object {
        $_ -match 'another Unity instance is running with this project open'
    }).Count -gt 0

    if ($unityExitCode -ne 0 -or $compilerErrors.Count -gt 0 -or $projectLockDetected) {
        Write-Host ""
        Write-Host "Compilation Check Report"
        Write-Host "------------------------"
        Write-Host "Status:  Blocked"
        Write-Host ""
        Write-Host "Details"
        Write-Host "-------"
        Write-Host ("Unity exited with code {0}." -f $unityExitCode)

        if ($projectLockDetected) {
            Write-Host "Project lock detected. Another Unity instance may be holding this project."
        }

        if ($compilerErrors.Count -gt 0) {
            Write-Host "Compiler errors:"
            $compilerErrors | ForEach-Object { Write-Host $_ }
        } elseif ($logLines.Count -gt 0) {
            Write-Host ((Get-Content $logPath -Tail 40) -join [Environment]::NewLine)
        } else {
            Write-Host "No Unity log was written."
        }

        $scriptExitCode = 1
        return
    }

    Write-Host ""
    Write-Host "Compilation Check Report"
    Write-Host "------------------------"
    Write-Host "Status:  PASS"
    $scriptExitCode = 0
}
catch {
    Write-Host ""
    Write-Host "Compilation Check Report"
    Write-Host "------------------------"
    Write-Host "Status:  Blocked"
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
