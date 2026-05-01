# LiveOps: compare cold vs warm `dotnet` build/publish and list Roslyn source-generator outputs.
# LiveOps uses `LiveOps/Directory.Build.props` to place bin/obj under `LiveOps/.artifacts/` (not per-project `obj/`).
# Usage (repo root):
#   pwsh -NoProfile -File .agents/scripts/test-liveops-deploy-cold-warm.ps1 [-DeleteArtifacts]
# -DeleteArtifacts: remove `LiveOps/.artifacts` before the timed steps (strongest "first time on disk" sim).
[CmdletBinding()]
param(
    [switch] $DeleteArtifacts
)
$ErrorActionPreference = "Stop"
Set-Location (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$gen = "Generators\Scaffold.LiveOps.Bootstrap.Generators\Scaffold.LiveOps.Bootstrap.Generators.csproj"
$sln = "LiveOps\LiveOps.Deploy.sln"
$csproj = "LiveOps\Deploy\LiveOps\LiveOps.csproj"
$artifacts = "LiveOps\.artifacts"

function Invoke-Timed {
    param([string]$Label, [scriptblock]$Action)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    & $Action
    $code = $LASTEXITCODE
    $sw.Stop()
    Write-Host ("[{0:n1}s] {1} (exit {2})" -f $sw.Elapsed.TotalSeconds, $Label, $code)
    if ($code -ne 0) { exit $code }
}

if ($DeleteArtifacts) {
    Write-Host "=== 0) Delete LiveOps/.artifacts (bin+obj) ==="
    if (Test-Path $artifacts) {
        Remove-Item -Path $artifacts -Recurse -Force
    }
    Write-Host "Removed: $artifacts"
}

Write-Host "=== 1) Clean LiveOps.Deploy.sln (Release) ==="
Invoke-Timed "dotnet clean" { dotnet clean $sln -c Release -v q }

Write-Host "=== 2) Cold: build generator (Release) ==="
Invoke-Timed "dotnet build generator" { dotnet build $gen -c Release -v q }

Write-Host "=== 3) Cold: build LiveOps.Deploy.sln (Release) ==="
Invoke-Timed "dotnet build sln (cold)" { dotnet build $sln -c Release -v q }

Write-Host "=== 4) Warm: build LiveOps.Deploy.sln again ==="
Invoke-Timed "dotnet build sln (warm)" { dotnet build $sln -c Release -v q }

Write-Host "=== 5) Cold: dotnet publish LiveOps.csproj (linux-x64, no self-contained) ==="
Invoke-Timed "dotnet publish (cold)" {
    dotnet publish $csproj -c Release -r linux-x64 --no-self-contained -v q
}

Write-Host "=== 6) Warm: dotnet publish again ==="
Invoke-Timed "dotnet publish (warm)" {
    dotnet publish $csproj -c Release -r linux-x64 --no-self-contained -v q
}

Write-Host "=== 7) Source-generated *.g.cs under LiveOps/.artifacts, sample (paths truncated) ==="
$g = Get-ChildItem -Path "LiveOps\.artifacts" -Recurse -Filter "*.g.cs" -ErrorAction SilentlyContinue
$g | Select-Object -First 25 @{ N = "FullName"; E = { if ($_.FullName.Length -gt 120) { $_.FullName.Substring(0, 117) + "..." } else { $_.FullName } } }
$manifest = $g | Where-Object { $_.Name -eq "LiveOpsManifest.g.cs" } | Select-Object -First 1
if ($manifest) {
    Write-Host "Found LiveOpsManifest.g.cs: $($manifest.FullName)"
} else {
    Write-Warning "LiveOpsManifest.g.cs not found under LiveOps/.artifacts (generator may not have run)."
}

Write-Host "=== Done ==="
