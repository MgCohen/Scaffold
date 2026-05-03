# Syncs GraphFlow Roslyn artifacts into Assets/Packages/com.scaffold.graphflow (run after changing the generator).
# Usage (repo root): powershell -NoProfile -ExecutionPolicy Bypass -File "Generators/Scaffold.GraphFlow/sync-unity-dlls.ps1"
$ErrorActionPreference = "Stop"
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin = Join-Path $repo "Generators/Scaffold.GraphFlow.PackageGenerator/bin/Release/netstandard2.0"
$runtime = Join-Path $repo "Assets/Packages/com.scaffold.graphflow/Runtime"
$generators = Join-Path $repo "Assets/Packages/com.scaffold.graphflow/Generators"
if (-not (Test-Path (Join-Path $bin "Scaffold.GraphFlow.PackageGenerator.dll"))) {
    throw "Build Generators first: dotnet build Generators/Scaffold.GraphFlow.PackageGenerator/Scaffold.GraphFlow.PackageGenerator.csproj -c Release"
}
# Sweep any prior attribute DLL filenames so Unity doesn't see two copies after a rename.
foreach ($stale in @("Scaffold.GraphFlow.Attributes.dll", "Scaffold.GraphFlow.PackageAttributes.dll")) {
    Remove-Item (Join-Path $runtime $stale) -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $runtime "$stale.meta") -ErrorAction SilentlyContinue
}
Copy-Item (Join-Path $bin "Scaffold.GraphFlow.AttributesLib.dll") $runtime -Force
Copy-Item (Join-Path $bin "Scaffold.GraphFlow.PackageGenerator.dll") $generators -Force
Write-Host "Copied AttributesLib + PackageGenerator DLLs into com.scaffold.graphflow."
