$Sln = "C:/Users/user/Documents/Unity/Scaffold/Scaffold.sln"

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
