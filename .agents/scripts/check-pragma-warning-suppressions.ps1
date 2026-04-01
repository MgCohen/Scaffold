[CmdletBinding()]
param(
    [string]$ProjectPath = (Get-Location).Path,
    [string]$AllowlistPath = ".agents/scripts/pragma-warning-disable-allowlist.txt"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-RepoPath {
    param([string]$Path)
    return $Path.Replace("\", "/").TrimStart("./")
}

function Resolve-GitExecutable {
    $gitCommand = Get-Command git -ErrorAction SilentlyContinue
    if ($gitCommand -and -not [string]::IsNullOrWhiteSpace($gitCommand.Path)) {
        return $gitCommand.Path
    }

    $candidatePaths = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Git\cmd\git.exe"),
        (Join-Path $env:ProgramFiles "Git\cmd\git.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Git\cmd\git.exe")
    )

    foreach ($candidatePath in $candidatePaths) {
        if (-not [string]::IsNullOrWhiteSpace($candidatePath) -and (Test-Path $candidatePath)) {
            return $candidatePath
        }
    }

    return $null
}

function Read-AllowlistPatterns {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedProjectPath,
        [Parameter(Mandatory = $true)]
        [string]$AllowlistRelativePath
    )

    $patterns = New-Object System.Collections.Generic.List[string]
    $resolvedAllowlistPath = Join-Path $ResolvedProjectPath $AllowlistRelativePath
    if (-not (Test-Path $resolvedAllowlistPath)) {
        return $patterns
    }

    foreach ($line in Get-Content -Path $resolvedAllowlistPath) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        if ($trimmed.StartsWith("#")) {
            continue
        }

        $patterns.Add((Normalize-RepoPath -Path $trimmed)) | Out-Null
    }

    return ,$patterns
}

function Is-AllowlistedFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoPath,
        [object]$Patterns
    )

    if ($null -eq $Patterns) {
        return $false
    }

    $enumerablePatterns = @($Patterns)
    if ($enumerablePatterns.Count -eq 0) {
        return $false
    }

    $normalized = Normalize-RepoPath -Path $RepoPath
    foreach ($pattern in $enumerablePatterns) {
        if ($normalized -like $pattern) {
            return $true
        }
    }

    return $false
}

function Is-CSharpFilePath {
    param([string]$RepoPath)
    if ([string]::IsNullOrWhiteSpace($RepoPath)) {
        return $false
    }

    $normalized = Normalize-RepoPath -Path $RepoPath
    return $normalized.EndsWith(".cs", [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-DisableIds {
    param([string]$LineText)

    if ([string]::IsNullOrWhiteSpace($LineText)) {
        return @()
    }

    $match = [regex]::Match($LineText, "(?i)#pragma\s+warning\s+disable\s+(?<ids>[A-Za-z0-9_,\s]+)")
    if (-not $match.Success) {
        return @()
    }

    return $match.Groups["ids"].Value.Split(",", [System.StringSplitOptions]::RemoveEmptyEntries) |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
}

function Is-CommentLine {
    param([string]$LineText)
    return $LineText.TrimStart().StartsWith("//")
}

function Has-ReasonComment {
    param(
        [string[]]$AllLines,
        [int]$DisableIndex
    )

    if ($DisableIndex -lt 0 -or $DisableIndex -ge $AllLines.Length) {
        return $false
    }

    $disableLine = $AllLines[$DisableIndex]
    if ($disableLine -match "//") {
        return $true
    }

    if ($DisableIndex -eq 0) {
        return $false
    }

    $previousLine = $AllLines[$DisableIndex - 1].Trim()
    return -not [string]::IsNullOrWhiteSpace($previousLine) -and (Is-CommentLine -LineText $previousLine)
}

function Has-ImmediateRestore {
    param(
        [string[]]$AllLines,
        [int]$DisableIndex,
        [string[]]$DisableIds
    )

    if ($DisableIndex -lt 0 -or $DisableIndex -ge $AllLines.Length) {
        return $false
    }

    $nextIndex = $DisableIndex + 1
    if ($nextIndex -ge $AllLines.Length) {
        return $false
    }

    $nextLine = $AllLines[$nextIndex].Trim()
    if ([string]::IsNullOrWhiteSpace($nextLine)) {
        return $false
    }

    $restoreMatch = [regex]::Match($nextLine, "(?i)^#pragma\s+warning\s+restore\s+(?<ids>[A-Za-z0-9_,\s]+)$")
    if (-not $restoreMatch.Success) {
        return $false
    }

    $restoreIds = $restoreMatch.Groups["ids"].Value.Split(",", [System.StringSplitOptions]::RemoveEmptyEntries) |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    if ($DisableIds.Count -eq 0 -or $restoreIds.Count -eq 0) {
        return $false
    }

    foreach ($disableId in $DisableIds) {
        if (-not ($restoreIds -contains $disableId)) {
            return $false
        }
    }

    return $true
}

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path
$allowlistPatterns = Read-AllowlistPatterns -ResolvedProjectPath $resolvedProjectPath -AllowlistRelativePath $AllowlistPath
$issues = New-Object System.Collections.Generic.List[object]
$gitExecutable = Resolve-GitExecutable

if ([string]::IsNullOrWhiteSpace($gitExecutable)) {
    Write-Output "TOTAL:-1"
    Write-Output "ISSUE:GitNotFound|n/a|n/a|Could not locate git executable."
    exit 1
}

Push-Location $resolvedProjectPath
try {
    $gitMarker = Join-Path $resolvedProjectPath ".git"
    if (-not (Test-Path $gitMarker)) {
        Write-Output "TOTAL:0"
        Write-Output "NOTE:Pragma gate skipped (no .git metadata at project root)."
        exit 0
    }

    $diffOutput = & $gitExecutable diff --unified=0 --no-color
    $gitExitCode = if (Test-Path variable:LASTEXITCODE) { [int]$LASTEXITCODE } else { 1 }
    if ($gitExitCode -ne 0) {
        Write-Output "TOTAL:-1"
        Write-Output ("ISSUE:GitDiffFailed|n/a|n/a|git diff failed with exit code {0}" -f $gitExitCode)
        exit 1
    }

    $currentFile = ""
    $currentLineNumber = 0

    foreach ($line in $diffOutput) {
        if ($line -match "^\+\+\+\s+b/(?<path>.+)$") {
            $currentFile = Normalize-RepoPath -Path $matches["path"]
            continue
        }

        if ($line -match "^@@\s+-\d+(?:,\d+)?\s+\+(?<start>\d+)(?:,\d+)?\s+@@") {
            $currentLineNumber = [int]$matches["start"]
            continue
        }

        if ($line.StartsWith("+") -and -not $line.StartsWith("+++")) {
            $addedContent = $line.Substring(1)
            if ($addedContent -match "(?i)#pragma\s+warning\s+disable\b") {
                if (-not (Is-CSharpFilePath -RepoPath $currentFile)) {
                    $currentLineNumber += 1
                    continue
                }

                if (-not [string]::IsNullOrWhiteSpace($currentFile) -and -not (Is-AllowlistedFile -RepoPath $currentFile -Patterns $allowlistPatterns)) {
                    $issues.Add([pscustomobject]@{
                            Type = "NewDisableNotAllowlisted"
                            File = $currentFile
                            Line = $currentLineNumber
                            Detail = "New '#pragma warning disable' is blocked unless file path is allowlisted."
                        })

                    $fullPath = Join-Path $resolvedProjectPath ($currentFile -replace "/", "\")
                    if (-not (Test-Path $fullPath)) {
                        $issues.Add([pscustomobject]@{
                                Type = "MissingFileForPragmaCheck"
                                File = $currentFile
                                Line = $currentLineNumber
                                Detail = "File not found while validating pragma suppression line."
                            })
                    } else {
                        $lines = Get-Content -Path $fullPath
                        $disableIndex = $currentLineNumber - 1
                        $disableIds = @(Get-DisableIds -LineText $addedContent)

                        if (-not (Has-ReasonComment -AllLines $lines -DisableIndex $disableIndex)) {
                            $issues.Add([pscustomobject]@{
                                    Type = "MissingReasonComment"
                                    File = $currentFile
                                    Line = $currentLineNumber
                                    Detail = "New '#pragma warning disable' must include a one-line reason comment (same line or previous line)."
                                })
                        }

                        if (-not (Has-ImmediateRestore -AllLines $lines -DisableIndex $disableIndex -DisableIds $disableIds)) {
                            $issues.Add([pscustomobject]@{
                                    Type = "MissingImmediateRestore"
                                    File = $currentFile
                                    Line = $currentLineNumber
                                    Detail = "New '#pragma warning disable' must be followed immediately by matching '#pragma warning restore <ID>'."
                                })
                        }
                    }
                }
            }

            $currentLineNumber += 1
            continue
        }

        if ($line.StartsWith("-") -and -not $line.StartsWith("---")) {
            continue
        }

        if (-not [string]::IsNullOrWhiteSpace($line)) {
            $currentLineNumber += 1
        }
    }
}
finally {
    Pop-Location
}

$sortedIssues = @($issues | Sort-Object -Property Type, File, Line)
Write-Output ("TOTAL:{0}" -f $sortedIssues.Count)

foreach ($issue in $sortedIssues) {
    Write-Output ("ISSUE:{0}|{1}|{2}|{3}" -f $issue.Type, $issue.File, $issue.Line, $issue.Detail)
}

if ($sortedIssues.Count -gt 0) {
    exit 1
}

exit 0
