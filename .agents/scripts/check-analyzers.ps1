[CmdletBinding()]
param(
    [string]$ProjectPath = (Get-Location).Path,
    [string]$SolutionPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-SolutionPath {
    param(
        [string]$RequestedSolutionPath,
        [string]$RequestedProjectPath
    )

    if ($RequestedSolutionPath) {
        if (-not (Test-Path $RequestedSolutionPath)) {
            throw "Solution path does not exist: '$RequestedSolutionPath'."
        }

        return (Resolve-Path $RequestedSolutionPath).Path
    }

    if (-not (Test-Path $RequestedProjectPath)) {
        throw "Project path does not exist: '$RequestedProjectPath'."
    }

    $resolvedProjectPath = (Resolve-Path $RequestedProjectPath).Path
    $preferred = Join-Path $resolvedProjectPath "Scaffold.sln"
    if (Test-Path $preferred) {
        return $preferred
    }

    $solutions = @(Get-ChildItem -Path $resolvedProjectPath -Filter *.sln -File)
    if ($solutions.Count -eq 0) {
        throw "No solution file found under '$resolvedProjectPath'."
    }

    return $solutions[0].FullName
}

$Sln = Resolve-SolutionPath -RequestedSolutionPath $SolutionPath -RequestedProjectPath $ProjectPath

# Builds the solution and prints deduplicated SCA diagnostics.
# Output format (parseable):
#   TOTAL:<n>
#   RULE:<code>:<count>
#   FILE:<relative-path>:<count>
#   BLOCKER:<raw error line>
$buildOutput = & dotnet build $Sln --no-incremental 2>&1 | ForEach-Object { "$_" }

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

    if ($line -match "Scaffold[/\\](.+?\.cs)\(") {
        $file = $matches[1] -replace "\\", "/"
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

$blockers = $buildOutput |
    Where-Object { $_ -match ": error " -and $_ -notmatch "SCA" -and $_ -notmatch "MSB" }

foreach ($line in $blockers) {
    Write-Output "BLOCKER:$line"
}
