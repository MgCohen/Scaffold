# Rewrites legacy Assets/Scripts module paths to Assets/Packages/com.scaffold.* in *.md files.
param([string]$Root = (Join-Path (Split-Path $PSScriptRoot -Parent) ".."))
$Root = (Resolve-Path $Root).Path
$repl = @(
    @("Assets/Scripts/Assets/Addressables", "Assets/Packages/com.scaffold.addressables"),
    @("Assets/Scripts/App/Bootstrap", "Assets/Packages/com.scaffold.bootstrap"),
    @("Assets/Scripts/App/View", "Assets/Packages/com.scaffold.view"),
    @("Assets/Scripts/Core/ViewModel", "Assets/Packages/com.scaffold.viewmodel"),
    @("Assets/Scripts/Core/LiveOps", "Assets/Packages/com.scaffold.liveops"),
    @("Assets/Scripts/Core/Entities", "Assets/Packages/com.scaffold.entities"),
    @("Assets/Scripts/Infra/Navigation", "Assets/Packages/com.scaffold.navigation"),
    @("Assets/Scripts/Infra/SceneFlow", "Assets/Packages/com.scaffold.sceneflow"),
    @("Assets/Scripts/Infra/CloudCode", "Assets/Packages/com.scaffold.cloudcode"),
    @("Assets/Scripts/Infra/Ugs", "Assets/Packages/com.scaffold.ugs"),
    @("Assets/Scripts/Infra/Events", "Assets/Packages/com.scaffold.events"),
    @("Assets/Scripts/Infra/Model", "Assets/Packages/com.scaffold.model"),
    @("Assets/Scripts/Infra/MVVM", "Assets/Packages/com.scaffold.mvvm"),
    @("Assets/Scripts/Infra/Scope", "Assets/Packages/com.scaffold.scope"),
    @("Assets/Scripts/Tools/Records", "Assets/Packages/com.scaffold.records"),
    @("Assets/Scripts/Tools/Maps", "Assets/Packages/com.scaffold.maps"),
    @("Assets/Scripts/Tools/Types", "Assets/Packages/com.scaffold.types"),
    @("Assets/Scripts/Core/MVVM", "Assets/Packages/com.scaffold.viewmodel")
)
Get-ChildItem -Path $Root -Recurse -Filter "*.md" -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch "\\Library\\|\\obj\\|\\bin\\|\\.git\\" } |
    ForEach-Object {
        $text = [System.IO.File]::ReadAllText($_.FullName)
        $out = $text
        foreach ($pair in $repl) {
            $out = $out.Replace($pair[0], $pair[1])
        }
        if ($out -ne $text) {
            [System.IO.File]::WriteAllText($_.FullName, $out)
            Write-Host $_.FullName
        }
    }
Write-Host "Rewrite done."
