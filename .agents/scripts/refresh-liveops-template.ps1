# Scaffold repo: build the Roslyn generator, then sync LiveOps/ into each package's Backend~/ from repo sources.
# Host: Assets/Packages/com.scaffold.liveops/Backend~ (Deploy/**, liveops manifest template, LiveOps.Deploy.sln).
# Features: e.g. com.scaffold.ads/Backend~/Scaffold/Ads* from LiveOps/Scaffold/.
# Plain file I/O; Backend~ is not a Unity import folder.
# Usage: pwsh -NoProfile -File .agents/scripts/refresh-liveops-template.ps1 [-DryRun] [-SkipGeneratorBuild]
# -SkipGeneratorBuild: only sync (used from MSBuild after the generator already built and copied its DLL).

[CmdletBinding()]
param(
    [switch] $DryRun,
    [switch] $SkipGeneratorBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$liveOps = Join-Path $repoRoot "LiveOps"
$genProj = Join-Path $repoRoot "Generators\Scaffold.LiveOps.Bootstrap.Generators\Scaffold.LiveOps.Bootstrap.Generators.csproj"

$excludeDirs = @("bin", "obj", ".vs", ".artifacts")
$roboExcludes = @()
foreach ($d in $excludeDirs) { $roboExcludes += @("/xd", $d) }

Write-Host "Repository root: $repoRoot"
if ($SkipGeneratorBuild) {
    Write-Host "SkipGeneratorBuild: not building generator (expect MSBuild CopyToLiveOpsDeployTools or manual build already updated Deploy/Tools/Generators)."
}
else {
    Write-Host "Building generator: $genProj"
    if (-not $DryRun) {
        & dotnet build $genProj -c Release -v q
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $genDll = Join-Path $repoRoot "Generators\Scaffold.LiveOps.Bootstrap.Generators\bin\Release\netstandard2.0\Scaffold.LiveOps.Bootstrap.Generators.dll"
        if (-not (Test-Path $genDll)) { throw "Generator DLL not found: $genDll" }
        $toolsGen = Join-Path $liveOps "Deploy\Tools\Generators"
        New-Item -ItemType Directory -Path $toolsGen -Force | Out-Null
        Copy-Item -Path $genDll -Destination (Join-Path $toolsGen "Scaffold.LiveOps.Bootstrap.Generators.dll") -Force
    }
}

$packagesRoot = Join-Path $repoRoot "Assets\Packages"
if (-not (Test-Path $packagesRoot)) { throw "Packages folder not found: $packagesRoot" }

foreach ($pkg in Get-ChildItem -Path $packagesRoot -Directory) {
    $back = Join-Path $pkg.FullName "Backend~"
    if (-not (Test-Path $back)) { continue }

    foreach ($top in Get-ChildItem -Path $back -Directory) {
        $topName = $top.Name
        if ($topName -ne "Deploy" -and $topName -ne "Scaffold") { continue }

        $liveTop = Join-Path $liveOps $topName
        if (-not (Test-Path $liveTop)) { Write-Warning "LiveOps side missing, skip: $liveTop (package $($pkg.Name))"; continue }

        foreach ($mod in Get-ChildItem -Path $top.FullName -Directory) {
            $src = Join-Path $liveTop $mod.Name
            if (-not (Test-Path $src)) {
                Write-Warning "No source in repo (skip): $src"
                continue
            }
            $dst = $mod.FullName
            Write-Host "Sync $src -> $dst"
            if ($DryRun) { continue }
            New-Item -ItemType Directory -Path $dst -Force | Out-Null
            $args = @($src, $dst, "/MIR", "/NFL", "/NDL", "/NJH", "/NJS", "/R:3", "/W:1") + $roboExcludes
            $p = Start-Process -FilePath "robocopy" -ArgumentList $args -NoNewWindow -PassThru -Wait
            if ($p.ExitCode -ge 8) { throw "robocopy failed with exit code $($p.ExitCode) for $src" }
        }
    }
}

$hostBack = Join-Path $repoRoot "Assets\Packages\com.scaffold.liveops\Backend~"
$manifestTemplate = Join-Path $hostBack "liveops.manifest.template.json"
$manifestSource = Join-Path $liveOps "liveops.manifest.json"
if (Test-Path $manifestSource) {
    if ($DryRun) { Write-Host "Would copy $manifestSource -> $manifestTemplate" }
    else { Copy-Item -Path $manifestSource -Destination $manifestTemplate -Force }
}

$deploySlnSource = Join-Path $liveOps "LiveOps.Deploy.sln"
$deploySlnTemplate = Join-Path $hostBack "LiveOps.Deploy.sln"
if (Test-Path $deploySlnSource) {
    if ($DryRun) { Write-Host "Would copy $deploySlnSource -> $deploySlnTemplate" }
    else { Copy-Item -Path $deploySlnSource -Destination $deploySlnTemplate -Force }
}

Write-Host "Done. Host template root: $hostBack"
exit 0
