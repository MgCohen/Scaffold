[CmdletBinding()]
param(
    [string]$ProjectPath = (Get-Location).Path,
    [string]$UnityPath,
    [int]$TimeoutMinutes = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-UnityVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedProjectPath
    )

    $versionFile = Join-Path $ResolvedProjectPath "ProjectSettings/ProjectVersion.txt"
    if (-not (Test-Path $versionFile)) {
        throw "Could not find ProjectVersion.txt at '$versionFile'."
    }

    $match = Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(.+)$'
    if (-not $match) {
        throw "Could not read m_EditorVersion from '$versionFile'."
    }

    return $match.Matches[0].Groups[1].Value.Trim()
}

function Resolve-UnityPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedProjectPath,
        [string]$RequestedUnityPath
    )

    $attempted = @()

    if ($RequestedUnityPath) {
        $attempted += ("-UnityPath: {0}" -f $RequestedUnityPath)
        if (-not (Test-Path $RequestedUnityPath)) {
            throw "Unity executable was not found at '$RequestedUnityPath'."
        }

        return (Resolve-Path $RequestedUnityPath).Path
    }

    if ($env:UNITY_PATH) {
        $attempted += ("UNITY_PATH: {0}" -f $env:UNITY_PATH)
        if (-not (Test-Path $env:UNITY_PATH)) {
            throw ("UNITY_PATH points to a missing file: '{0}'." -f $env:UNITY_PATH)
        }

        return (Resolve-Path $env:UNITY_PATH).Path
    }

    $version = Get-UnityVersion -ResolvedProjectPath $ResolvedProjectPath
    $candidateRoots = @(
        (Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Unity\Hub\Editor\$version\Editor\Unity.exe")
    ) | Where-Object { $_ }

    $attempted += $candidateRoots
    $candidates = @($candidateRoots | Where-Object { Test-Path $_ })

    if ($candidates.Count -gt 0) {
        return $candidates[0]
    }

    $attemptSummary = $attempted | ForEach-Object { "  - $_" }
    $examplePath = "<path-to-Unity.exe>"
    $exampleCommand = ".\.agents\scripts\check-unity-compilation.ps1 -UnityPath `"$examplePath`""
    $message = @(
        ("Unity executable could not be resolved for version '{0}'." -f $version),
        "Attempted locations:",
        ($attemptSummary -join [Environment]::NewLine),
        "Fix by passing -UnityPath explicitly, for example:",
        ("  {0}" -f $exampleCommand)
    ) -join [Environment]::NewLine
    throw $message
}

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

    $resolvedUnityPath = Resolve-UnityPath -ResolvedProjectPath $resolvedProjectPath -RequestedUnityPath $UnityPath
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

    $unityProcess = Start-Process -FilePath $resolvedUnityPath -ArgumentList $unityArgs -PassThru -NoNewWindow
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
