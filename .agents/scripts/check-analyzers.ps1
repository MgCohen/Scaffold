[CmdletBinding()]
param(
    [string]$ProjectPath = (Get-Location).Path,
    [int]$TimeoutMinutes = 10,
    [int]$AnalyzerTestsTimeoutMinutes = 10,
    [switch]$IncludeTestAssemblies
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path
$analyzerTestsProjectPath = Join-Path $resolvedProjectPath "Analyzers/Scaffold/Scaffold.Analyzers.Tests/Scaffold.Analyzers.Tests.csproj"
$mvvmAnalyzerTestsProjectPath = Join-Path $resolvedProjectPath "Generators/Scaffold.Mvvm.Analyzers.Tests/Scaffold.Mvvm.Analyzers.Tests.csproj"

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

function Get-ProjectPathFromBuildLine {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Line
    )

    if ($Line -match "\[(?<project>[^\]]+\.csproj)\]\s*$") {
        return $matches['project']
    }

    return $null
}

function Is-TestAssemblyProject {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
    return $projectName -match "(?i)(^|[._-])(tests?|playmodetests?|editmodetests?)([._-]|$)"
}

function Should-IncludeBuildLine {
    param(
        [string]$Line,
        [Parameter(Mandatory = $true)]
        [bool]$IncludeTests
    )

    if ([string]::IsNullOrWhiteSpace($Line)) {
        return $true
    }

    if ($IncludeTests) {
        return $true
    }

    $projectPath = Get-ProjectPathFromBuildLine -Line $Line
    if ([string]::IsNullOrWhiteSpace($projectPath)) {
        return $true
    }

    return -not (Is-TestAssemblyProject -ProjectPath $projectPath)
}

function Escape-CmdDoubleQuotes {
    param([Parameter(Mandatory = $true)][string]$Text)
    return $Text.Replace('"', '""')
}

function Invoke-CmdDotNet {
    <#
        Runs `dotnet ...` under cmd.exe with merged stdout/stderr to a log file (avoids pipe deadlocks
        and matches real exit codes). Returns @{ ExitCode = int; LogPath = string }.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$DotNetArguments,
        [Parameter(Mandatory = $true)]
        [string]$LogFilePath,
        [int]$TimeoutMilliseconds = -1
    )

    $escapedArgs = @()
    foreach ($a in $DotNetArguments) {
        if ($null -eq $a) {
            continue
        }
        if ($a -match '[\s"]') {
            $escapedArgs += ('"' + (Escape-CmdDoubleQuotes -Text $a) + '"')
        } else {
            $escapedArgs += $a
        }
    }

    $dotnetLine = "dotnet " + ($escapedArgs -join " ")
    $logEsc = Escape-CmdDoubleQuotes -Text $LogFilePath
    $tail = "$dotnetLine > `"$logEsc`" 2>&1"

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "cmd.exe"
    $psi.Arguments = "/c $tail"
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $false
    $psi.RedirectStandardError = $false

    $p = New-Object System.Diagnostics.Process
    $p.StartInfo = $psi
    [void]$p.Start()

    if ($TimeoutMilliseconds -gt 0) {
        $didExit = $p.WaitForExit($TimeoutMilliseconds)
        if (-not $didExit) {
            try {
                $p.Kill()
            } catch {
            }

            return @{ ExitCode = -1; LogPath = $LogFilePath }
        }
    } else {
        $p.WaitForExit()
    }

    return @{ ExitCode = [int]$p.ExitCode; LogPath = $LogFilePath }
}

# Runs analyzer unit tests, then builds the solution and prints deduplicated Scaffold analyzer diagnostics (SCA + SCM).
# Output format (parseable):
#   BUILD_EXIT:<code>
#   TOTAL:<n>                    (SCA + SCM rule hits combined)
#   RULE:<code>:<count>
#   FILE:<relative-path>:<count>
#   DIAG:<raw diagnostic line>
#   BLOCKER:<raw error line>
$analyzerTestsProjects = @(
    @{ Path = $analyzerTestsProjectPath; Label = "Scaffold.Analyzers.Tests" }
    @{ Path = $mvvmAnalyzerTestsProjectPath; Label = "Scaffold.Mvvm.Analyzers.Tests" }
) | Where-Object { Test-Path $_.Path }

if ($analyzerTestsProjects.Count -eq 0) {
    Write-Output ("NOTE:No analyzer test projects found (e.g. '{0}'). Analyzer unit tests skipped." -f $analyzerTestsProjectPath)
}

if ($analyzerTestsProjects.Count -gt 0) {
    if ($AnalyzerTestsTimeoutMinutes -lt 1) {
        Write-Output "TOTAL:-1"
        Write-Output "BLOCKER:AnalyzerTestsTimeoutMinutes must be 1 or greater."
        exit 1
    }

    foreach ($testsProject in $analyzerTestsProjects) {
        $analyzerTestsOutput = @()
        $testsTempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("dotnet-test-analyzers-" + [guid]::NewGuid().ToString("N"))
        $null = New-Item -ItemType Directory -Path $testsTempRoot -Force
        $testsLogPath = Join-Path $testsTempRoot "dotnet.log"
        $analyzerTestsExitCode = 1

        try {
            $testsTimeoutMilliseconds = $AnalyzerTestsTimeoutMinutes * 60 * 1000
            $testRun = Invoke-CmdDotNet `
                -DotNetArguments @("test", $testsProject.Path, "-c", "Release", "--nologo") `
                -LogFilePath $testsLogPath `
                -TimeoutMilliseconds $testsTimeoutMilliseconds

            $analyzerTestsExitCode = $testRun.ExitCode

            if ($analyzerTestsExitCode -eq -1) {
                Write-Output "TOTAL:-1"
                Write-Output ("BLOCKER:Analyzer tests ({0}) timed out after {1} minute(s)." -f $testsProject.Label, $AnalyzerTestsTimeoutMinutes)
                exit 1
            }

            if (Test-Path $testsLogPath) {
                $analyzerTestsOutput += @(Get-Content $testsLogPath -ErrorAction SilentlyContinue)
            }
        }
        finally {
            if (Test-Path $testsTempRoot) {
                try {
                    [System.IO.Directory]::Delete($testsTempRoot, $true)
                } catch {
                    Start-Sleep -Milliseconds 250
                    try {
                        [System.IO.Directory]::Delete($testsTempRoot, $true)
                    } catch {
                    }
                }
            }
        }

        if ($analyzerTestsExitCode -ne 0) {
            Write-Output "TOTAL:-1"
            Write-Output ("BLOCKER:Analyzer tests ({0}) failed (exit code {1})." -f $testsProject.Label, $analyzerTestsExitCode)
            foreach ($line in $analyzerTestsOutput) {
                if ([string]::IsNullOrWhiteSpace($line)) { continue }
                Write-Output ("BLOCKER:{0}" -f $line)
            }
            exit 1
        }

        Write-Output ("NOTE:Analyzer tests passed ({0})." -f $testsProject.Label)
    }
}

$selectedSolution = Resolve-SolutionPath -ResolvedProjectPath $resolvedProjectPath
if ($null -eq $selectedSolution) {
    Write-Output "TOTAL:0"
    Write-Output "NOTE:No .sln file found at project root. Analyzer check skipped."
    exit 0
}

Write-Output ("NOTE:Using solution '{0}'." -f $selectedSolution.Name)
if (-not $IncludeTestAssemblies.IsPresent) {
    Write-Output "NOTE:Excluding diagnostics from test assemblies (use -IncludeTestAssemblies to include them)."
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("dotnet-build-check-" + [guid]::NewGuid().ToString("N"))
$null = New-Item -ItemType Directory -Path $tempRoot -Force
$buildLogPath = Join-Path $tempRoot "build.log"
$buildOutput = @()
$buildExitCode = -1

try {
    if ($TimeoutMinutes -lt 1) {
        throw "TimeoutMinutes must be 1 or greater."
    }

    $buildTimeoutMilliseconds = $TimeoutMinutes * 60 * 1000
    $buildRun = Invoke-CmdDotNet `
        -DotNetArguments @("build", $selectedSolution.FullName, "--no-incremental") `
        -LogFilePath $buildLogPath `
        -TimeoutMilliseconds $buildTimeoutMilliseconds

    $buildExitCode = $buildRun.ExitCode

    if ($buildExitCode -eq -1) {
        Write-Output "BUILD_EXIT:-1"
        Write-Output "TOTAL:-1"
        Write-Output ("BLOCKER:Analyzer build timed out after {0} minute(s)." -f $TimeoutMinutes)
        exit 1
    }

    if (Test-Path $buildLogPath) {
        $buildOutput += @(Get-Content $buildLogPath -ErrorAction SilentlyContinue)
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

Write-Output "BUILD_EXIT:$buildExitCode"

$filteredBuildOutput = $buildOutput |
    Where-Object { Should-IncludeBuildLine -Line $_ -IncludeTests $IncludeTestAssemblies.IsPresent }

# SCA = Scaffold.Analyzers; SCM = Scaffold.Mvvm.Analyzers (MVVM pack)
$analyzerDiagnosticPattern = ": (warning|error) (SCA[0-9]+|SCM[0-9]+)"
$scaffoldAnalyzerLines = $filteredBuildOutput |
    Where-Object { $_ -match $analyzerDiagnosticPattern } |
    Sort-Object -Unique

$total = if ($null -eq $scaffoldAnalyzerLines) { 0 } elseif ($scaffoldAnalyzerLines -is [array]) { $scaffoldAnalyzerLines.Count } else { 1 }
Write-Output "TOTAL:$total"

$ruleCounts = @{}
$fileCounts = @{}

foreach ($line in $scaffoldAnalyzerLines) {
    if ($line -match "\b(SCA[0-9]+|SCM[0-9]+)\b") {
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

foreach ($line in $scaffoldAnalyzerLines) {
    Write-Output "DIAG:$line"
}

# Compiler / tooling errors (not SCA/SCM analyzer codes). MSBuild engine errors use MSBxxxx.
$blockers = $filteredBuildOutput |
    Where-Object { $_ -match ": error " -and $_ -notmatch ": error SCA[0-9]+" -and $_ -notmatch ": error SCM[0-9]+" -and $_ -notmatch "MSB" } |
    Sort-Object -Unique

foreach ($line in $blockers) {
    Write-Output "BLOCKER:$line"
}

$scriptFailed = $false
if ($buildExitCode -ne 0) {
    $scriptFailed = $true
    Write-Output ("NOTE:Solution build failed with exit code {0}." -f $buildExitCode)
}

if (@($blockers).Count -gt 0) {
    $scriptFailed = $true
}

if ($scriptFailed) {
    exit 1
}
