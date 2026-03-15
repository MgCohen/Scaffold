[CmdletBinding()]
param(
    [string]$ProjectPath = (Get-Location).Path,
    [int]$TimeoutMinutes = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path

function Resolve-SolutionPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedProjectPath
    )

    $solutionFiles = @(Get-ChildItem -Path $ResolvedProjectPath -Filter "*.sln" -File | Sort-Object -Property FullName)
    if ($solutionFiles.Count -eq 0) {
        return $null
    }

    if ($solutionFiles.Count -eq 1) {
        return $solutionFiles[0]
    }

    $projectFolderName = Split-Path -Path $ResolvedProjectPath -Leaf
    $preferredName = "$projectFolderName.sln"
    $preferredMatch = $solutionFiles | Where-Object { $_.Name -ieq $preferredName } | Select-Object -First 1
    if ($preferredMatch) {
        return $preferredMatch
    }

    return $solutionFiles[0]
}

function Try-GetRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,
        [Parameter(Mandatory = $true)]
        [string]$CandidatePath
    )

    try {
        $resolvedCandidate = (Resolve-Path $CandidatePath -ErrorAction Stop).Path
    } catch {
        return $CandidatePath -replace "\\", "/"
    }

    $baseUri = New-Object System.Uri(($BasePath.TrimEnd('\') + '\'))
    $candidateUri = New-Object System.Uri($resolvedCandidate)
    if ($baseUri.IsBaseOf($candidateUri)) {
        $relative = $baseUri.MakeRelativeUri($candidateUri).ToString()
        return [System.Uri]::UnescapeDataString($relative) -replace "\\", "/"
    }

    return $resolvedCandidate -replace "\\", "/"
}

# Builds the solution and prints deduplicated SCA diagnostics.
# Output format (parseable):
#   TOTAL:<n>
#   RULE:<code>:<count>
#   FILE:<relative-path>:<count>
#   DIAG:<raw SCA diagnostic line>
#   BLOCKER:<raw error line>
$selectedSolution = Resolve-SolutionPath -ResolvedProjectPath $resolvedProjectPath
if ($null -eq $selectedSolution) {
    Write-Output "TOTAL:0"
    Write-Output "NOTE:No .sln file found at project root. Analyzer check skipped."
    exit 0
}

Write-Output ("NOTE:Using solution '{0}'." -f $selectedSolution.Name)
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("dotnet-build-check-" + [guid]::NewGuid().ToString("N"))
$null = New-Item -ItemType Directory -Path $tempRoot -Force
$stdOutPath = Join-Path $tempRoot "stdout.log"
$stdErrPath = Join-Path $tempRoot "stderr.log"
$buildOutput = @()

try {
    if ($TimeoutMinutes -lt 1) {
        throw "TimeoutMinutes must be 1 or greater."
    }

    $buildProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("build", $selectedSolution.FullName, "--no-incremental") `
        -PassThru `
        -NoNewWindow `
        -RedirectStandardOutput $stdOutPath `
        -RedirectStandardError $stdErrPath

    $timeoutMilliseconds = $TimeoutMinutes * 60 * 1000
    $didExit = $buildProcess.WaitForExit($timeoutMilliseconds)

    if (-not $didExit) {
        try {
            Stop-Process -Id $buildProcess.Id -Force -ErrorAction SilentlyContinue
        } catch {
        }

        Write-Output "TOTAL:-1"
        Write-Output ("BLOCKER:Analyzer build timed out after {0} minute(s)." -f $TimeoutMinutes)
        exit 1
    }

    if (Test-Path $stdOutPath) {
        $buildOutput += @(Get-Content $stdOutPath)
    }

    if (Test-Path $stdErrPath) {
        $buildOutput += @(Get-Content $stdErrPath)
    }
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

$scaLines = $buildOutput |
    Where-Object { $_ -match ": (warning|error) SCA[0-9]+" } |
    Sort-Object -Unique

$total = if ($null -eq $scaLines) { 0 } elseif ($scaLines -is [array]) { $scaLines.Count } else { 1 }
Write-Output "TOTAL:$total"

$ruleCounts = @{}
$fileCounts = @{}

foreach ($line in $scaLines) {
    if ($line -match "\b(SCA[0-9]+)\b") {
        $rule = $matches[1]
        if ($ruleCounts.ContainsKey($rule)) {
            $ruleCounts[$rule] += 1
        } else {
            $ruleCounts[$rule] = 1
        }
    }

    $file = $null
    if ($line -match "(?<path>[A-Za-z]:\\[^:(]+\.cs)\(") {
        $file = Try-GetRelativePath -BasePath $resolvedProjectPath -CandidatePath $matches['path']
    } elseif ($line -match "(?<path>[^:\s][^:()]*\.cs)\(") {
        $file = ($matches['path'] -replace "\\", "/")
    }

    if ($file) {
        if ($fileCounts.ContainsKey($file)) {
            $fileCounts[$file] += 1
        } else {
            $fileCounts[$file] = 1
        }
    }
}

foreach ($entry in $ruleCounts.GetEnumerator() | Sort-Object -Property @{Expression='Value';Descending=$true}, @{Expression='Key';Descending=$false}) {
    Write-Output "RULE:$($entry.Key):$($entry.Value)"
}

foreach ($entry in $fileCounts.GetEnumerator() | Sort-Object -Property @{Expression='Value';Descending=$true}, @{Expression='Key';Descending=$false}) {
    Write-Output "FILE:$($entry.Key):$($entry.Value)"
}

foreach ($line in $scaLines) {
    Write-Output "DIAG:$line"
}

$blockers = $buildOutput |
    Where-Object { $_ -match ": error " -and $_ -notmatch "SCA" -and $_ -notmatch "MSB" }

foreach ($line in $blockers) {
    Write-Output "BLOCKER:$line"
}
