[CmdletBinding()]
param(
    [string]$ProjectPath = (Get-Location).Path,
    [string]$UnityPath,
    [string[]]$AssemblyNames,
    [switch]$EnableCoverage,
    [string]$CoverageResultsPath,
    [string]$CoverageOptions,
    [int]$TimeoutMinutes = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path (Split-Path -Parent $PSCommandPath) "UnityProcess.ps1")
. (Join-Path (Split-Path -Parent $PSCommandPath) "..\testing\TestingSuite.Config.ps1")

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
    $hubRoots = @(
        (Join-Path $env:ProgramFiles "Unity\Hub\Editor"),
        (Join-Path ${env:ProgramFiles(x86)} "Unity\Hub\Editor")
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique

    $candidateRoots = @(
        (Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Unity\Hub\Editor\$version\Editor\Unity.exe")
    ) | Where-Object { $_ }

    $attempted += $candidateRoots
    $candidates = @($candidateRoots | Where-Object { Test-Path $_ })

    if ($candidates.Count -gt 0) {
        return $candidates[0]
    }

    $installed = @()
    foreach ($hubRoot in $hubRoots) {
        $installed += @(Get-ChildItem -Path $hubRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            $unityExe = Join-Path $_.FullName "Editor\Unity.exe"
            if (-not (Test-Path $unityExe)) {
                return
            }

            $name = $_.Name
            $rank = $null
            if ($name -match '^(\d+)\.(\d+)\.(\d+)([a-z])(\d+)$') {
                $channel = $matches[4]
                $channelRank = switch ($channel) {
                    "a" { 1 }
                    "b" { 2 }
                    "f" { 3 }
                    "p" { 4 }
                    default { 0 }
                }

                $rank = [pscustomobject]@{
                    Major = [int]$matches[1]
                    Minor = [int]$matches[2]
                    Patch = [int]$matches[3]
                    Channel = $channelRank
                    Build = [int]$matches[5]
                }
            } else {
                $rank = [pscustomobject]@{
                    Major = -1
                    Minor = -1
                    Patch = -1
                    Channel = -1
                    Build = -1
                }
            }

            [pscustomobject]@{
                VersionLabel = $name
                UnityExe = $unityExe
                Rank = $rank
            }
        })
    }

    if ($installed.Count -gt 0) {
        $fallback = $installed | Sort-Object `
            @{ Expression = { $_.Rank.Major }; Descending = $true }, `
            @{ Expression = { $_.Rank.Minor }; Descending = $true }, `
            @{ Expression = { $_.Rank.Patch }; Descending = $true }, `
            @{ Expression = { $_.Rank.Channel }; Descending = $true }, `
            @{ Expression = { $_.Rank.Build }; Descending = $true }, `
            @{ Expression = { $_.VersionLabel }; Descending = $true } | Select-Object -First 1

        Write-Host ("Requested Unity version '{0}' was not found. Falling back to installed '{1}'." -f $version, $fallback.VersionLabel)
        return $fallback.UnityExe
    }

    $attemptSummary = $attempted | ForEach-Object { "  - $_" }
    $examplePath = "<path-to-Unity.exe>"
    $exampleCommand = ".\.agents\scripts\run-playmode-tests.ps1 -UnityPath `"$examplePath`""
    $message = @(
        ("Unity executable could not be resolved for version '{0}'." -f $version),
        "Attempted locations:",
        ($attemptSummary -join [Environment]::NewLine),
        "Fix by passing -UnityPath explicitly, for example:",
        ("  {0}" -f $exampleCommand)
    ) -join [Environment]::NewLine
    throw $message
}

function Get-TestMessage {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlNode]$Node
    )

    $failureNode = $Node.SelectSingleNode("failure/message")
    if (-not $failureNode) {
        return ""
    }

    return ($failureNode.InnerText -replace '\s+', ' ').Trim()
}

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path
$testingSuiteConfig = Get-TestingSuiteConfig -ProjectPath $resolvedProjectPath
$resolvedUnityPath = $null
$resolvedCoveragePath = $null
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("unity-playmode-tests-" + [guid]::NewGuid().ToString("N"))
$null = New-Item -ItemType Directory -Path $tempRoot -Force
$logPath = Join-Path $tempRoot "playmode.log"
$resultsPath = Join-Path $tempRoot "playmode-results.xml"
$scriptExitCode = 1

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

    $resolvedUnityPath = Resolve-UnityPath -ResolvedProjectPath $resolvedProjectPath -RequestedUnityPath $UnityPath
    $unityArgs = @(
        "-batchmode"
        "-accept-apiupdate"
        "-projectPath", $resolvedProjectPath
        "-runTests"
        "-testPlatform", "PlayMode"
        "-testResults", $resultsPath
        "-logFile", $logPath
    )

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

    Write-Host "Running Unity PlayMode tests..."
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
        $scriptExitCode = 1
        return
    }

    $unityExitCode = [int]$unityProcess.ExitCode

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
            $message = Get-TestMessage -Node $failedNode
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
