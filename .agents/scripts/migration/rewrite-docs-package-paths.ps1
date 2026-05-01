# Rewrites legacy Assets/Scripts module paths to Assets/Packages/com.scaffold.* in *.md files.
# Paths are loaded from package-path-mappings.json (same folder as this script).
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
$repl = @($data.markdownReplacements)

Get-ChildItem -Path $Root -Recurse -Filter "*.md" -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch "\\Library\\|\\obj\\|\\bin\\|\\.git\\" } |
    ForEach-Object {
        $text = [System.IO.File]::ReadAllText($_.FullName)
        $out = $text
        foreach ($pair in $repl) {
            $out = $out.Replace($pair.from, $pair.to)
        }
        if ($out -ne $text) {
            [System.IO.File]::WriteAllText($_.FullName, $out)
            Write-Host $_.FullName
        }
    }
Write-Host "Rewrite done."
