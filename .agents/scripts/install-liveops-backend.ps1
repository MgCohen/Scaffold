# Install or update repo-root LiveOps from every package that ships Backend~ (merge into LiveOps/).
# com.scaffold.liveops/Backend~ carries Deploy/**, LiveOps.Deploy.sln, and the manifest template.
# Feature packages (e.g. com.scaffold.ads) carry Backend~/Scaffold/<Feature>/.
# Preserves LiveOps/Game/**.
# Unity: Scaffold > LiveOps > Install or Update Backend runs the same flow in com.scaffold.liveops (Scaffold.LiveOps.Editor, LiveOpsBackendInstall) without this script.
# Usage (from repo root): pwsh -NoProfile -File .agents/scripts/install-liveops-backend.ps1 [-DryRun] [-ProjectRoot <path>]

[CmdletBinding()]
param(
    [string] $ProjectRoot = (Get-Location).Path,
    [switch] $DryRun
)

$ErrorActionPreference = "Stop"

# Keep in sync with LiveOpsBackendInstall.MapCsprojToSolutionFolder (Assets/Packages/com.scaffold.liveops/Editor/LiveOpsBackendInstall.cs).
function Strip-DtoParentFolderSuffix {
    param([string] $FolderName)
    if ($FolderName.EndsWith('.DTO', [StringComparison]::OrdinalIgnoreCase)) {
        return $FolderName.Substring(0, $FolderName.Length - 4)
    }
    return $FolderName
}

function Map-CsprojToSolutionFolder {
    param(
        [string] $SlnDir,
        [string] $CsprojFullPath
    )
    $slnFull = [IO.Path]::GetFullPath($SlnDir)
    $projFull = [IO.Path]::GetFullPath($CsprojFullPath)
    $rel = [IO.Path]::GetRelativePath($slnFull, $projFull)
    if ($rel.StartsWith('..', [StringComparison]::Ordinal)) {
        throw "csproj is not under solution directory: $CsprojFullPath"
    }
    $parts = @($rel -split '[\\/]+' | Where-Object { $_ -ne '' })
    if ($parts.Count -lt 2) {
        throw "Unexpected csproj depth relative to solution: $rel"
    }
    $top = $parts[0]
    if ($top -ieq 'Deploy') {
        $second = $parts[1]
        if ($second -ieq 'Core') { return 'Deploy/Core' }
        if ($second -ieq 'LiveOps') {
            $fileName = $parts[$parts.Count - 1]
            if ($fileName -ieq 'LiveOps.csproj') { return 'Deploy/Host' }
            return "Deploy/$second"
        }
        return "Deploy/$second"
    }
    if ($top -ieq 'Scaffold') {
        $module = Strip-DtoParentFolderSuffix $parts[1]
        return "Scaffold/$module"
    }
    if ($top -ieq 'Game') {
        $module = Strip-DtoParentFolderSuffix $parts[1]
        return "Game/$module"
    }
    throw "Unrecognized LiveOps.Deploy.sln project layout (expected Deploy/, Scaffold/, or Game/): $rel"
}

$packagesRoot = Join-Path $ProjectRoot "Assets\Packages"
$destLive = Join-Path $ProjectRoot "LiveOps"
$hostBack = Join-Path $ProjectRoot "Assets\Packages\com.scaffold.liveops\Backend~"

if (-not (Test-Path $hostBack)) {
    throw "Host Backend~ not found: $hostBack (install com.scaffold.liveops with Backend~, or run refresh-liveops-template.ps1 in the Scaffold repo)."
}

$excludeDirs = @("bin", "obj", ".vs", ".artifacts")
$roboExcludes = @()
foreach ($d in $excludeDirs) { $roboExcludes += @("/xd", $d) }

$packages = Get-ChildItem -Path $packagesRoot -Directory -ErrorAction SilentlyContinue
if (-not $packages) { throw "No packages under: $packagesRoot" }

$syncedAny = $false
foreach ($pkg in $packages) {
    $back = Join-Path $pkg.FullName "Backend~"
    if (-not (Test-Path $back)) { continue }
    $syncedAny = $true
    Write-Host "Merge Backend~ from $($pkg.Name) -> $destLive [$(if ($DryRun) { 'dry-run' } else { 'apply' })]"
    if ($DryRun) { continue }
    $args = @($back, $destLive, "/E", "/NFL", "/NDL", "/NJH", "/NJS", "/R:3", "/W:1") + $roboExcludes
    $p = Start-Process -FilePath "robocopy" -ArgumentList $args -NoNewWindow -PassThru -Wait
    if ($p.ExitCode -ge 8) { throw "robocopy failed with exit code $($p.ExitCode) for package $($pkg.Name)" }
}

if (-not $syncedAny) {
    throw "No Assets/Packages/*/Backend~ folders found (expected at least com.scaffold.liveops/Backend~)."
}

$game = Join-Path $destLive "Game"
if (-not (Test-Path $game)) {
    if ($DryRun) { Write-Host "Would create: $game" }
    else { New-Item -ItemType Directory -Path $game -Force | Out-Null }
}

$deploySln = Join-Path $destLive "LiveOps.Deploy.sln"
$templateDeploySln = Join-Path $hostBack "LiveOps.Deploy.sln"
if (Test-Path $templateDeploySln) {
    if ($DryRun) { Write-Host "Would copy $templateDeploySln -> $deploySln" }
    else { Copy-Item -Path $templateDeploySln -Destination $deploySln -Force }
}

# Sync the deploy solution against on-disk reality:
#   - prune: drop Project entries whose .csproj does not exist (e.g. peer Scaffold packages opted out)
#   - add: register every LiveOps/Scaffold/** + LiveOps/Game/** csproj (excluding *.Tests.csproj)
# Mirrors LiveOpsBackendInstall.PruneMissingProjectsFromSolution + EnsureDiscoveredProjectsInSolution.
# Requires `dotnet` on PATH; on failure we warn-and-continue because the build still resolves these
# via Scaffold.LiveOps.Deploy.targets globs.
if (-not $DryRun -and (Test-Path $deploySln)) {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        Write-Warning "dotnet not on PATH — skipping LiveOps.Deploy.sln prune+add. Build still works via Scaffold.LiveOps.Deploy.targets globs."
    }
    else {
        $slnDir = Split-Path -Parent $deploySln
        $projectLineRegex = '^Project\("\{[0-9A-Fa-f-]+\}"\)\s*=\s*"[^"]*",\s*"([^"]+\.csproj)",\s*"(\{[0-9A-Fa-f-]+\})"\s*$'
        $nestedLineRegex = '^\s*(\{[0-9A-Fa-f-]+\})\s*=\s*\{[0-9A-Fa-f-]+\}\s*$'
        $existing = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
        $prunedGuids = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        foreach ($line in Get-Content -LiteralPath $deploySln) {
            $m = [System.Text.RegularExpressions.Regex]::Match($line, $projectLineRegex)
            if (-not $m.Success) { continue }
            $rel = $m.Groups[1].Value.Replace('\', [IO.Path]::DirectorySeparatorChar).Replace('/', [IO.Path]::DirectorySeparatorChar)
            $full = [IO.Path]::GetFullPath((Join-Path $slnDir $rel))
            if (-not (Test-Path -LiteralPath $full)) {
                [void]$prunedGuids.Add($m.Groups[2].Value)
                & dotnet sln $deploySln remove $full | Out-Null
            }
            else {
                [void]$existing.Add($full)
            }
        }
        if ($prunedGuids.Count -gt 0) {
            $outLines = New-Object System.Collections.Generic.List[string]
            foreach ($slLine in [IO.File]::ReadAllLines($deploySln)) {
                $nm = [regex]::Match($slLine, $nestedLineRegex)
                if ($nm.Success -and $prunedGuids.Contains($nm.Groups[1].Value)) { continue }
                [void]$outLines.Add($slLine)
            }
            $utf8NoBom = New-Object System.Text.UTF8Encoding $false
            [IO.File]::WriteAllLines($deploySln, $outLines.ToArray(), $utf8NoBom)
        }
        $candidates = @()
        foreach ($sub in @("Scaffold", "Game")) {
            $root = Join-Path $destLive $sub
            if (-not (Test-Path -LiteralPath $root)) { continue }
            $candidates += Get-ChildItem -LiteralPath $root -Recurse -Filter *.csproj -File -ErrorAction SilentlyContinue |
                Where-Object { -not $_.Name.EndsWith('.Tests.csproj', [StringComparison]::OrdinalIgnoreCase) }
        }
        $toAdd = @()
        foreach ($f in $candidates) {
            if (-not $existing.Contains([IO.Path]::GetFullPath($f.FullName))) { $toAdd += $f }
        }
        if ($toAdd.Count -gt 0) {
            $byFolder = @{}
            foreach ($f in $toAdd) {
                $folder = Map-CsprojToSolutionFolder -SlnDir $slnDir -CsprojFullPath $f.FullName
                if (-not $byFolder.ContainsKey($folder)) {
                    $byFolder[$folder] = New-Object System.Collections.Generic.List[string]
                }
                $byFolder[$folder].Add($f.FullName)
            }
            $synced = 0
            foreach ($folder in $byFolder.Keys) {
                $paths = $byFolder[$folder]
                & dotnet sln $deploySln add --solution-folder $folder @paths
                if ($LASTEXITCODE -ne 0) {
                    Write-Warning "dotnet sln add returned $LASTEXITCODE (folder $folder); build still works via Scaffold.LiveOps.Deploy.targets globs."
                }
                else {
                    $synced += $paths.Count
                }
            }
            if ($synced -gt 0) {
                Write-Host "Synced $synced feature/game project(s) into $deploySln (by solution folder)"
            }
        }
    }
}

Write-Host "Game tree preserved. Cloud Code (.ccmr): use $(Join-Path $destLive 'LiveOps.Deploy.sln') (not LiveOps.sln, which includes tests and can exceed the 10MB upload cap)."
if (-not $DryRun) {
    Write-Host "Optional: dotnet build `"$destLive\Deploy\LiveOps\LiveOps.csproj`" -c Release"
}
exit 0
