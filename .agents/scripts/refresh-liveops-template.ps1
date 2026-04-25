# Scaffold repo: build the Roslyn generator, then copy LiveOps/Deploy and LiveOps/Scaffold into
# Assets/Packages/com.scaffold.liveops/Template~/LiveOps/ (excludes bin/obj). Plain file I/O; Template~ is not a Unity import folder.
# Usage: pwsh -NoProfile -File .agents/scripts/refresh-liveops-template.ps1 [-DryRun] [-SkipGeneratorBuild]
# -SkipGeneratorBuild: only robocopy Deploy/Scaffold + manifest + LiveOps.Deploy.sln (used from MSBuild after the generator already built and copied its DLL).

[CmdletBinding()]
param(
    [switch] $DryRun,
    [switch] $SkipGeneratorBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$liveOps = Join-Path $repoRoot "LiveOps"
$templateRoot = Join-Path $repoRoot "Assets\Packages\com.scaffold.liveops\Template~\LiveOps"
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

$copyPairs = @(
    @{ Src = (Join-Path $liveOps "Deploy"); Dst = (Join-Path $templateRoot "Deploy") },
    @{ Src = (Join-Path $liveOps "Scaffold"); Dst = (Join-Path $templateRoot "Scaffold") }
)

foreach ($pair in $copyPairs) {
    if (-not (Test-Path $pair.Src)) { throw "Missing source: $($pair.Src)" }
    Write-Host "Sync $($pair.Src) -> $($pair.Dst)"
    if ($DryRun) { continue }
    New-Item -ItemType Directory -Path $pair.Dst -Force | Out-Null
    $args = @($pair.Src, $pair.Dst, "/MIR", "/NFL", "/NDL", "/NJH", "/NJS") + $roboExcludes
    $p = Start-Process -FilePath "robocopy" -ArgumentList $args -NoNewWindow -PassThru -Wait
    if ($p.ExitCode -ge 8) { throw "robocopy failed with exit code $($p.ExitCode)" }
}

$manifestTemplate = Join-Path $templateRoot "liveops.manifest.template.json"
$manifestSource = Join-Path $liveOps "liveops.manifest.json"
if (Test-Path $manifestSource) {
    if ($DryRun) { Write-Host "Would copy $manifestSource -> $manifestTemplate" }
    else { Copy-Item -Path $manifestSource -Destination $manifestTemplate -Force }
}

$deploySlnSource = Join-Path $liveOps "LiveOps.Deploy.sln"
$deploySlnTemplate = Join-Path $templateRoot "LiveOps.Deploy.sln"
if (Test-Path $deploySlnSource) {
    if ($DryRun) { Write-Host "Would copy $deploySlnSource -> $deploySlnTemplate" }
    else { Copy-Item -Path $deploySlnSource -Destination $deploySlnTemplate -Force }
}

Write-Host "Done. Template root: $templateRoot"
exit 0
