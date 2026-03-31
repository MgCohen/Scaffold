# Updates Unity-generated *.csproj at repo root after moving sources to Assets/Packages/com.scaffold.*.
# Unity will regenerate these on next project open; this keeps dotnet/analyzer builds working until then.
param([string]$Root = (Join-Path (Split-Path $PSScriptRoot -Parent) ".."))
$Root = (Resolve-Path $Root).Path
$repl = @(
    @("Assets\Generators\MVVM\", "Assets\Packages\com.scaffold.mvvm\GeneratorsMVVM\"),
    @("Assets/Generators/MVVM/", "Assets/Packages/com.scaffold.mvvm/GeneratorsMVVM/"),
    @("Assets\Generators\Autopacker\", "Assets\Packages\com.scaffold.autopacker\"),
    @("Assets/Generators/Autopacker/", "Assets/Packages/com.scaffold.autopacker/"),
    @("Assets\Scripts\Assets\Addressables", "Assets\Packages\com.scaffold.addressables"),
    @("Assets/Scripts/Assets/Addressables", "Assets/Packages/com.scaffold.addressables"),
    @("Assets\Scripts\App\Bootstrap", "Assets\Packages\com.scaffold.bootstrap"),
    @("Assets/Scripts/App/Bootstrap", "Assets/Packages/com.scaffold.bootstrap"),
    @("Assets\Scripts\App\View", "Assets\Packages\com.scaffold.view"),
    @("Assets/Scripts/App/View", "Assets/Packages/com.scaffold.view"),
    @("Assets\Scripts\Core\Entities", "Assets\Packages\com.scaffold.entities"),
    @("Assets/Scripts/Core/Entities", "Assets/Packages/com.scaffold.entities"),
    @("Assets\Scripts\Core\LiveOps", "Assets\Packages\com.scaffold.liveops"),
    @("Assets/Scripts/Core/LiveOps", "Assets/Packages/com.scaffold.liveops"),
    @("Assets\Scripts\Core\ViewModel", "Assets\Packages\com.scaffold.viewmodel"),
    @("Assets/Scripts/Core/ViewModel", "Assets/Packages/com.scaffold.viewmodel"),
    @("Assets\Scripts\Infra\CloudCode", "Assets\Packages\com.scaffold.cloudcode"),
    @("Assets/Scripts/Infra/CloudCode", "Assets/Packages/com.scaffold.cloudcode"),
    @("Assets\Scripts\Infra\Events", "Assets\Packages\com.scaffold.events"),
    @("Assets/Scripts/Infra/Events", "Assets/Packages/com.scaffold.events"),
    @("Assets\Scripts\Infra\Model", "Assets\Packages\com.scaffold.model"),
    @("Assets/Scripts/Infra/Model", "Assets/Packages/com.scaffold.model"),
    @("Assets\Scripts\Infra\MVVM", "Assets\Packages\com.scaffold.mvvm"),
    @("Assets/Scripts/Infra/MVVM", "Assets/Packages/com.scaffold.mvvm"),
    @("Assets\Scripts\Infra\Navigation", "Assets\Packages\com.scaffold.navigation"),
    @("Assets/Scripts/Infra/Navigation", "Assets/Packages/com.scaffold.navigation"),
    @("Assets\Scripts\Infra\SceneFlow", "Assets\Packages\com.scaffold.sceneflow"),
    @("Assets/Scripts/Infra/SceneFlow", "Assets/Packages/com.scaffold.sceneflow"),
    @("Assets\Scripts\Infra\Scope", "Assets\Packages\com.scaffold.scope"),
    @("Assets/Scripts/Infra/Scope", "Assets/Packages/com.scaffold.scope"),
    @("Assets\Scripts\Infra\Ugs", "Assets\Packages\com.scaffold.ugs"),
    @("Assets/Scripts/Infra/Ugs", "Assets/Packages/com.scaffold.ugs"),
    @("Assets\Scripts\Tools\Maps", "Assets\Packages\com.scaffold.maps"),
    @("Assets/Scripts/Tools/Maps", "Assets/Packages/com.scaffold.maps"),
    @("Assets\Scripts\Tools\Records", "Assets\Packages\com.scaffold.records"),
    @("Assets/Scripts/Tools/Records", "Assets/Packages/com.scaffold.records"),
    @("Assets\Scripts\Tools\Types", "Assets\Packages\com.scaffold.types"),
    @("Assets/Scripts/Tools/Types", "Assets/Packages/com.scaffold.types")
)
Get-ChildItem -Path $Root -Filter "*.csproj" -File | ForEach-Object {
    $text = [System.IO.File]::ReadAllText($_.FullName)
    $out = $text
    foreach ($pair in $repl) {
        $out = $out.Replace($pair[0], $pair[1])
    }
    if ($out -ne $text) {
        [System.IO.File]::WriteAllText($_.FullName, $out)
        Write-Host $_.Name
    }
}
Write-Host "Done."
