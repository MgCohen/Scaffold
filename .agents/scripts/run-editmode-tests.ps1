[CmdletBinding()]
param(
    [string]$ProjectPath = (Get-Location).Path,
    [string]$UnityPath,
    [string[]]$AssemblyNames
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

    if ($RequestedUnityPath) {
        if (-not (Test-Path $RequestedUnityPath)) {
            throw "Unity executable was not found at '$RequestedUnityPath'."
        }

        return (Resolve-Path $RequestedUnityPath).Path
    }

    $version = Get-UnityVersion -ResolvedProjectPath $ResolvedProjectPath
    $candidates = @(@(
        (Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Unity\Hub\Editor\$version\Editor\Unity.exe")
    ) | Where-Object { $_ -and (Test-Path $_) })

    if ($candidates.Count -gt 0) {
        return $candidates[0]
    }

    throw "Unity executable could not be auto-detected for version '$version'. Pass -UnityPath explicitly."
}

function Get-TestMessage {
    param(
        [Parameter(Mandatory = $true)]
        [xml]$Node
    )

    $failureNode = $Node.SelectSingleNode("failure/message")
    if (-not $failureNode) {
        return ""
    }

    return ($failureNode.InnerText -replace '\s+', ' ').Trim()
}

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path
$resolvedUnityPath = Resolve-UnityPath -ResolvedProjectPath $resolvedProjectPath -RequestedUnityPath $UnityPath
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("scaffold-editmode-tests-" + [guid]::NewGuid().ToString("N"))
$null = New-Item -ItemType Directory -Path $tempRoot -Force
$logPath = Join-Path $tempRoot "editmode.log"
$resultsPath = Join-Path $tempRoot "editmode-results.xml"
$scriptExitCode = 1

$unityArgs = @(
    "-batchmode"
    "-accept-apiupdate"
    "-projectPath", $resolvedProjectPath
    "-runTests"
    "-testPlatform", "EditMode"
    "-testResults", $resultsPath
    "-logFile", $logPath
)

if ($AssemblyNames -and $AssemblyNames.Count -gt 0) {
    $unityArgs += @("-assemblyNames", ($AssemblyNames -join ";"))
}

try {
    Write-Host "Running Unity EditMode tests..."
    Write-Host "Project: $resolvedProjectPath"
    Write-Host "Unity:   $resolvedUnityPath"

    $unityProcess = Start-Process -FilePath $resolvedUnityPath -ArgumentList $unityArgs -PassThru -Wait -NoNewWindow
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
finally {
    if (Test-Path $tempRoot) {
        [System.IO.Directory]::Delete($tempRoot, $true)
    }
}

exit $scriptExitCode
