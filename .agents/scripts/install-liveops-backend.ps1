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

$installRecord = Join-Path $destLive ".scaffold-install.json"
$manifestOut = Join-Path $destLive "liveops.manifest.json"
$templateManifest = Join-Path $hostBack "liveops.manifest.template.json"
if (Test-Path $templateManifest) {
    if ($DryRun) { Write-Host "Would copy $templateManifest -> $manifestOut" }
    else { Copy-Item -Path $templateManifest -Destination $manifestOut -Force }
}

$ver = "0.0.0"
if (Test-Path (Join-Path $ProjectRoot "Assets\Packages\com.scaffold.liveops\package.json")) {
    $pkg = Get-Content (Join-Path $ProjectRoot "Assets\Packages\com.scaffold.liveops\package.json") -Raw | ConvertFrom-Json
    if ($pkg.version) { $ver = $pkg.version }
}
$installPayload = [ordered]@{
    templateVersion = $ver
    installedRoots  = @("LiveOps/Deploy", "LiveOps/Scaffold")
    lastUpdate      = [DateTime]::UtcNow.ToString("o")
    notes           = "Merged from Assets/Packages/*/Backend~ (host: com.scaffold.liveops/Backend~)"
}
if ($DryRun) { Write-Host "Would write: $installRecord" }
else { $installPayload | ConvertTo-Json -Depth 6 | Set-Content -Path $installRecord -Encoding UTF8 }

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
        $projectLineRegex = '^Project\("\{[0-9A-Fa-f-]+\}"\)\s*=\s*"[^"]*",\s*"([^"]+\.csproj)",\s*"\{[0-9A-Fa-f-]+\}"\s*$'
        $existing = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($line in Get-Content -LiteralPath $deploySln) {
            $m = [System.Text.RegularExpressions.Regex]::Match($line, $projectLineRegex)
            if (-not $m.Success) { continue }
            $rel = $m.Groups[1].Value.Replace('\', [IO.Path]::DirectorySeparatorChar).Replace('/', [IO.Path]::DirectorySeparatorChar)
            $full = [IO.Path]::GetFullPath((Join-Path $slnDir $rel))
            if (-not (Test-Path -LiteralPath $full)) {
                & dotnet sln $deploySln remove $full | Out-Null
            }
            else {
                [void]$existing.Add($full)
            }
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
            if (-not $existing.Contains([IO.Path]::GetFullPath($f.FullName))) { $toAdd += $f.FullName }
        }
        if ($toAdd.Count -gt 0) {
            & dotnet sln $deploySln add @toAdd
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "dotnet sln add returned $LASTEXITCODE; build still works via Scaffold.LiveOps.Deploy.targets globs."
            }
            else {
                Write-Host "Synced $($toAdd.Count) feature/game project(s) into $deploySln"
            }
        }
    }
}

Write-Host "Game tree preserved. Cloud Code (.ccmr): use $(Join-Path $destLive 'LiveOps.Deploy.sln') (not LiveOps.sln, which includes tests and can exceed the 10MB upload cap)."
if (-not $DryRun) {
    Write-Host "Optional: dotnet build `"$destLive\Deploy\LiveOps\LiveOps.csproj`" -c Release"
}
exit 0
