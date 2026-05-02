#!/usr/bin/env pwsh
# Emits a readable markdown summary (Time + Allocated + Gen0 medians) from Unity Performance Testing JSON (-perfTestResults).
# Example (repo root):
#   pwsh -NoProfile -File .agents/scripts/summarize-unity-performance-json.ps1 `
#     -JsonPath "Assets/Packages/com.scaffold.maps/PerformanceTestResults.json" `
#     -OutputPath "Assets/Packages/com.scaffold.maps/Tests/Performance/PerformanceTestResults.report.md"

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$JsonPath,
    [string]$ProjectPath = (Get-Location).Path,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resolvedRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
function Resolve-ProjectPath([string]$RelativeOrAbsolute) {
    if ([System.IO.Path]::IsPathRooted($RelativeOrAbsolute)) {
        return [System.IO.Path]::GetFullPath($RelativeOrAbsolute)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $resolvedRoot $RelativeOrAbsolute))
}

$jsonFull = Resolve-ProjectPath $JsonPath
$outFull = Resolve-ProjectPath $OutputPath

if (-not (Test-Path -LiteralPath $jsonFull)) {
    throw "JSON not found: $jsonFull"
}

$j = Get-Content -Raw -LiteralPath $jsonFull | ConvertFrom-Json

$rows = New-Object System.Collections.Generic.List[object]
foreach ($r in $j.Results) {
    $sg = @($r.SampleGroups)
    $t = $sg | Where-Object { $_.Name -like '*:Time' } | Select-Object -First 1
    $a = $sg | Where-Object { $_.Name -like '*:Allocated' } | Select-Object -First 1
    $c = $sg | Where-Object { $_.Name -like '*:AllocCount' } | Select-Object -First 1
    $g0 = $sg | Where-Object { $_.Name -like '*:Gen0' } | Select-Object -First 1
    if (-not $t) {
        continue
    }

    $rows.Add([PSCustomObject]@{
            Method     = $r.MethodName
            ClassName  = $r.ClassName
            FullName   = $r.Name
            TimeNs     = $t.Median
            AllocBytes = $a.Median
            AllocCount = if ($c) { $c.Median } else { $null }
            Gen0       = $g0.Median
        }) | Out-Null
}

$sb = [System.Text.StringBuilder]::new()
$null = $sb.AppendLine("# Performance run summary")
$null = $sb.AppendLine()
$null = $sb.AppendLine("Generated from Unity Performance Testing JSON. **`Alloc B/op`** = median bytes/op (best available counter — see `Bench.ByteSource`). **`Alloc/op`** = median count of `GC.Alloc` Profiler-marker events per op (works in EditMode regardless of byte-counter availability).")
$null = $sb.AppendLine()
$null = $sb.AppendLine("| Test (method) | Time (ns/op) | Alloc B/op | Alloc/op | Gen0 |")
$null = $sb.AppendLine("|---------------|--------------|------------|----------|------|")

foreach ($row in ($rows | Sort-Object ClassName, Method)) {
    $tn = [math]::Round([double]$row.TimeNs, 2)
    $ab = [math]::Round([double]$row.AllocBytes, 4)
    $ac = if ($null -ne $row.AllocCount) { [math]::Round([double]$row.AllocCount, 4) } else { "-" }
    $g0 = [math]::Round([double]$row.Gen0, 4)
    $null = $sb.AppendLine(("| `{0}` | {1} | {2} | {3} | {4} |" -f $row.Method, $tn, $ab, $ac, $g0))
}

$null = $sb.AppendLine()
$null = $sb.AppendLine("## Map vs Dictionary (same scenario)")
$null = $sb.AppendLine()
$null = $sb.AppendLine("| Scenario | Map Time | Map B/op | Map Allocs | Dict Time | Dict B/op | Dict Allocs |")
$null = $sb.AppendLine("|----------|----------|----------|------------|-----------|-----------|-------------|")

$mapRows = $rows | Where-Object { $_.Method -like 'Map_*' }
foreach ($mr in $mapRows) {
    $suffix = $mr.Method -replace '^Map_', ''
    $dr = $rows | Where-Object { $_.Method -eq ("Dict_$suffix") } | Select-Object -First 1
    if (-not $dr) {
        continue
    }

    $mAc = if ($null -ne $mr.AllocCount) { [math]::Round([double]$mr.AllocCount, 4) } else { "-" }
    $dAc = if ($null -ne $dr.AllocCount) { [math]::Round([double]$dr.AllocCount, 4) } else { "-" }
    $null = $sb.AppendLine(("| {0} | {1} | {2} | {3} | {4} | {5} | {6} |" -f `
                $suffix,
            ([math]::Round([double]$mr.TimeNs, 2)),
            ([math]::Round([double]$mr.AllocBytes, 4)),
            $mAc,
            ([math]::Round([double]$dr.TimeNs, 2)),
            ([math]::Round([double]$dr.AllocBytes, 4)),
            $dAc))
}

$outDir = Split-Path -Parent $outFull
if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -LiteralPath $outDir)) {
    $null = New-Item -ItemType Directory -Path $outDir -Force
}

[System.IO.File]::WriteAllText($outFull, $sb.ToString())
Write-Host "Wrote $outFull"
