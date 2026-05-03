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
# Avoid Unity seeing two attribute DLLs if the assembly was renamed.
Remove-Item (Join-Path $runtime "Scaffold.GraphFlow.Attributes.dll") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $runtime "Scaffold.GraphFlow.Attributes.dll.meta") -ErrorAction SilentlyContinue
Copy-Item (Join-Path $bin "Scaffold.GraphFlow.PackageAttributes.dll") $runtime -Force
Copy-Item (Join-Path $bin "Scaffold.GraphFlow.PackageGenerator.dll") $generators -Force
Write-Host "Copied Attributes + PackageGenerator DLLs into com.scaffold.graphflow."
