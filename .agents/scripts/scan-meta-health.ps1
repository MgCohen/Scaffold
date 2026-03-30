# Quick Unity .meta health scan: duplicate GUIDs, missing meta, orphan meta.
param(
    [string]$AssetsRoot = (Join-Path $PSScriptRoot "..\..\Assets" | Resolve-Path).Path
)

$ErrorActionPreference = "Continue"
$guidToPath = @{}
$duplicateLines = New-Object System.Collections.Generic.List[string]
$noGuidLines = New-Object System.Collections.Generic.List[string]
$emptyMeta = New-Object System.Collections.Generic.List[string]

Get-ChildItem -Path $AssetsRoot -Recurse -Filter "*.meta" -File -ErrorAction SilentlyContinue | ForEach-Object {
    $path = $_.FullName
    $raw = $null
    try { $raw = [System.IO.File]::ReadAllText($path) } catch { $emptyMeta.Add("Unreadable: $path"); return }
    if ([string]::IsNullOrWhiteSpace($raw)) { $emptyMeta.Add("Empty: $path"); return }
    $m = [regex]::Match($raw, "guid:\s*([a-f0-9]+)")
    if (-not $m.Success) { $noGuidLines.Add($path); return }
    $guid = $m.Groups[1].Value
    if ($guid.Length -ne 32) {
        $noGuidLines.Add("$path (guid length $($guid.Length), expected 32)")
        return
    }
    if ($guidToPath.ContainsKey($guid)) {
        $duplicateLines.Add("DUPLICATE GUID $guid")
        $duplicateLines.Add("  A: $($guidToPath[$guid])")
        $duplicateLines.Add("  B: $path")
    }
    else {
        $guidToPath[$guid] = $path
    }
}

Write-Host "=== Duplicate GUIDs ===" -ForegroundColor Cyan
if ($duplicateLines.Count -eq 0) { Write-Host "(none)" }
else { $duplicateLines | ForEach-Object { Write-Host $_ } }
Write-Host "Unique GUIDs indexed: $($guidToPath.Count)"

Write-Host "`n=== .meta with no valid guid line ===" -ForegroundColor Yellow
if ($noGuidLines.Count -eq 0) { Write-Host "(none)" }
else { $noGuidLines | ForEach-Object { Write-Host $_ } }

Write-Host "`n=== Empty / unreadable .meta ===" -ForegroundColor Yellow
if ($emptyMeta.Count -eq 0) { Write-Host "(none)" }
else { $emptyMeta | ForEach-Object { Write-Host $_ } }

$missingMeta = New-Object System.Collections.Generic.List[string]
Get-ChildItem -Path $AssetsRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
    $_.FullName -notmatch '\.meta$' -and $_.Name -ne ".DS_Store"
} | ForEach-Object {
    $meta = $_.FullName + ".meta"
    if (-not (Test-Path -LiteralPath $meta)) { $missingMeta.Add($_.FullName) }
}

Write-Host "`n=== Asset files missing .meta (first 80) ===" -ForegroundColor Cyan
Write-Host "Total: $($missingMeta.Count)"
$missingMeta | Select-Object -First 80 | ForEach-Object { Write-Host $_ }

$orphans = New-Object System.Collections.Generic.List[string]
Get-ChildItem -Path $AssetsRoot -Recurse -Filter "*.meta" -File -ErrorAction SilentlyContinue | ForEach-Object {
    $metaPath = $_.FullName
    if ($metaPath -match '^(.+)\.meta$') {
        $assetPath = $Matches[1]
        if (-not (Test-Path -LiteralPath $assetPath)) { $orphans.Add($metaPath) }
    }
}

Write-Host "`n=== Orphan .meta (no asset file) ===" -ForegroundColor Cyan
Write-Host "Total: $($orphans.Count)"
$orphans | ForEach-Object { Write-Host $_ }
