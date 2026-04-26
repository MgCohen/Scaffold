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
Write-Host "Game tree preserved. Cloud Code (.ccmr): use $(Join-Path $destLive 'LiveOps.Deploy.sln') (not LiveOps.sln, which includes tests and can exceed the 10MB upload cap)."
if (-not $DryRun) {
    Write-Host "Optional: dotnet build `"$destLive\Deploy\LiveOps\LiveOps.csproj`" -c Release"
}
exit 0
