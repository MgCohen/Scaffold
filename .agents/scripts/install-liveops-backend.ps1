# Install or update repo-root LiveOps from the com.scaffold.liveops package Template~ folder.
# Overwrites only LiveOps/Deploy/** and LiveOps/Scaffold/**. Preserves LiveOps/Game/**.
# Usage (from repo root): pwsh -NoProfile -File .agents/scripts/install-liveops-backend.ps1 [-DryRun] [-ProjectRoot <path>]

[CmdletBinding()]
param(
    [string] $ProjectRoot = (Get-Location).Path,
    [switch] $DryRun
)

$ErrorActionPreference = "Stop"
$template = Join-Path $ProjectRoot "Assets\Packages\com.scaffold.liveops\Template~\LiveOps"
$destLive = Join-Path $ProjectRoot "LiveOps"

if (-not (Test-Path $template)) {
    throw "Template not found: $template (install com.scaffold.liveops or run refresh-liveops-template.ps1 in Scaffold repo)."
}

$excludeDirs = @("bin", "obj", ".vs", ".artifacts")
$roboExcludes = @()
foreach ($d in $excludeDirs) { $roboExcludes += @("/xd", $d) }

$copyPairs = @(
    @{ Src = (Join-Path $template "Deploy"); Dst = (Join-Path $destLive "Deploy") },
    @{ Src = (Join-Path $template "Scaffold"); Dst = (Join-Path $destLive "Scaffold") }
)

foreach ($pair in $copyPairs) {
    if (-not (Test-Path $pair.Src)) { Write-Warning "Skip missing template source: $($pair.Src)"; continue }
    Write-Host "Sync $($pair.Src) -> $($pair.Dst) [$(if ($DryRun) { 'dry-run' } else { 'apply' })]"
    if ($DryRun) { continue }
    New-Item -ItemType Directory -Path $pair.Dst -Force | Out-Null
    $args = @($pair.Src, $pair.Dst, "/MIR", "/NFL", "/NDL", "/NJH", "/NJS") + $roboExcludes
    $p = Start-Process -FilePath "robocopy" -ArgumentList $args -NoNewWindow -PassThru -Wait
    if ($p.ExitCode -ge 8) { throw "robocopy failed with exit code $($p.ExitCode)" }
}

$game = Join-Path $destLive "Game"
if (-not (Test-Path $game)) {
    if ($DryRun) { Write-Host "Would create: $game" }
    else { New-Item -ItemType Directory -Path $game -Force | Out-Null }
}

$installRecord = Join-Path $destLive ".scaffold-install.json"
$manifestOut = Join-Path $destLive "liveops.manifest.json"
$templateManifest = Join-Path $template "liveops.manifest.template.json"
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
    notes           = $null
}
if ($DryRun) { Write-Host "Would write: $installRecord" }
else { $installPayload | ConvertTo-Json -Depth 6 | Set-Content -Path $installRecord -Encoding UTF8 }

$deploySln = Join-Path $destLive "LiveOps.Deploy.sln"
$templateDeploySln = Join-Path $template "LiveOps.Deploy.sln"
if (Test-Path $templateDeploySln) {
    if ($DryRun) { Write-Host "Would copy $templateDeploySln -> $deploySln" }
    else { Copy-Item -Path $templateDeploySln -Destination $deploySln -Force }
}
Write-Host "Game tree preserved. Cloud Code (.ccmr): use $(Join-Path $destLive 'LiveOps.Deploy.sln') (not LiveOps.sln, which includes tests and can exceed the 10MB upload cap)."
if (-not $DryRun) {
    Write-Host "Optional: dotnet build `"$destLive\Deploy\LiveOps\LiveOps.csproj`" -c Release"
}
exit 0
