# Updates Unity-generated *.csproj at repo root after moving sources to Assets/Packages/com.scaffold.*.
# Unity will regenerate these on next project open; this keeps dotnet/analyzer builds working until then.
param([string]$Root = "")
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
} else {
    $Root = (Resolve-Path $Root).Path
}

$jsonPath = Join-Path $PSScriptRoot "package-path-mappings.json"
$data = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
$repl = @($data.csprojReplacements)

Get-ChildItem -Path $Root -Filter "*.csproj" -File | ForEach-Object {
    $text = [System.IO.File]::ReadAllText($_.FullName)
    $out = $text
    foreach ($pair in $repl) {
        $out = $out.Replace($pair.from, $pair.to)
    }
    if ($out -ne $text) {
        [System.IO.File]::WriteAllText($_.FullName, $out)
        Write-Host $_.Name
    }
}
Write-Host "Done."
