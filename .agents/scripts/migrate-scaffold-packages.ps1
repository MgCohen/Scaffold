# Moves module folders to Assets/Packages/com.scaffold.* and writes package.json (holder layout).
# Run from repo root: powershell -NoProfile -ExecutionPolicy Bypass -File .\.agents\scripts\migrate-scaffold-packages.ps1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$destRoot = Join-Path $root "Assets\Packages"
$schemasGit = "https://github.com/ScaffoldLibrary/Schemas.git"

function New-ScaffoldPackageJson {
    param(
        [string]$Name,
        [string]$DisplayName,
        [string]$Description,
        [hashtable]$Dependencies = @{}
    )
    $obj = [ordered]@{
        name         = $Name
        version      = "0.1.0"
        displayName  = $DisplayName
        description  = $Description
        unity        = "6000.0"
        dependencies = $Dependencies
    }
    (($obj | ConvertTo-Json -Depth 8) -replace "`r`n", "`n") + "`n"
}

$moves = @(
    @{ Rel = "Assets\Scripts\Tools\Records"; Id = "com.scaffold.records" }
    @{ Rel = "Assets\Scripts\Tools\Maps"; Id = "com.scaffold.maps" }
    @{ Rel = "Assets\Scripts\Infra\Scope"; Id = "com.scaffold.scope" }
    @{ Rel = "Assets\Scripts\Infra\CloudCode"; Id = "com.scaffold.cloudcode" }
    @{ Rel = "Assets\Scripts\Infra\SceneFlow"; Id = "com.scaffold.sceneflow" }
    @{ Rel = "Assets\Scripts\Infra\Ugs"; Id = "com.scaffold.ugs" }
    @{ Rel = "Assets\Scripts\Infra\Events"; Id = "com.scaffold.events" }
    @{ Rel = "Assets\Scripts\Infra\MVVM"; Id = "com.scaffold.mvvm" }
    @{ Rel = "Assets\Scripts\Infra\Model"; Id = "com.scaffold.model" }
    @{ Rel = "Assets\Scripts\Core\Entities"; Id = "com.scaffold.entities" }
    @{ Rel = "Assets\Scripts\Core\ViewModel"; Id = "com.scaffold.viewmodel" }
    @{ Rel = "Assets\Scripts\Core\LiveOps"; Id = "com.scaffold.liveops" }
    @{ Rel = "Assets\Scripts\Assets\Addressables"; Id = "com.scaffold.addressables" }
    @{ Rel = "Assets\Scripts\Infra\Navigation"; Id = "com.scaffold.navigation" }
    @{ Rel = "Assets\Scripts\App\View"; Id = "com.scaffold.view" }
    @{ Rel = "Assets\Scripts\App\Bootstrap"; Id = "com.scaffold.bootstrap" }
)

foreach ($m in $moves) {
    $src = Join-Path $root $m.Rel
    $dst = Join-Path $destRoot $m.Id
    if (-not (Test-Path $src)) {
        Write-Warning "Skip missing source: $src"
        continue
    }
    if (Test-Path $dst) {
        Write-Warning "Destination exists, skip: $dst"
        continue
    }
    New-Item -ItemType Directory -Path $destRoot -Force | Out-Null
    Move-Item -Path $src -Destination $dst -Force
    Write-Host "Moved $($m.Rel) -> $($m.Id)"
}

$mvvmGenSrc = Join-Path $root "Assets\Generators\MVVM"
$mvvmPkg = Join-Path $destRoot "com.scaffold.mvvm"
$mvvmGenDst = Join-Path $mvvmPkg "GeneratorsMVVM"
if ((Test-Path $mvvmGenSrc) -and (Test-Path $mvvmPkg) -and -not (Test-Path $mvvmGenDst)) {
    Move-Item -Path $mvvmGenSrc -Destination $mvvmGenDst -Force
    Write-Host "Moved Assets/Generators/MVVM -> com.scaffold.mvvm/GeneratorsMVVM"
}

$apSrc = Join-Path $root "Assets\Generators\Autopacker"
$apDst = Join-Path $destRoot "com.scaffold.autopacker"
if ((Test-Path $apSrc) -and -not (Test-Path $apDst)) {
    Move-Item -Path $apSrc -Destination $apDst -Force
    Write-Host "Moved Assets/Generators/Autopacker -> com.scaffold.autopacker"
}

$packageSpecs = @{
    "com.scaffold.records"      = @{ Display = "Scaffold Records"; Desc = "Record utilities for Scaffold."; Deps = @{} }
    "com.scaffold.maps"         = @{ Display = "Scaffold Maps"; Desc = "Map/index utilities for Scaffold."; Deps = @{ "com.scaffold.records" = "0.1.0" } }
    "com.scaffold.scope"        = @{ Display = "Scaffold Scope"; Desc = "Layered scope and DI composition helpers."; Deps = @{} }
    "com.scaffold.cloudcode"    = @{ Display = "Scaffold Cloud Code"; Desc = "Unity Cloud Code integration."; Deps = @{} }
    "com.scaffold.sceneflow"    = @{ Display = "Scaffold Scene Flow"; Desc = "Scene flow and Addressables scene loading."; Deps = @{ "com.unity.addressables" = "2.9.1" } }
    "com.scaffold.ugs"          = @{ Display = "Scaffold UGS"; Desc = "Unity Gaming Services integration."; Deps = @{ "com.scaffold.scope" = "0.1.0" } }
    "com.scaffold.events"       = @{ Display = "Scaffold Events"; Desc = "Event bus and messaging."; Deps = @{ "com.scaffold.types" = "0.1.0" } }
    "com.scaffold.mvvm"         = @{ Display = "Scaffold MVVM"; Desc = "MVVM core and MVVM-related generators in this repo."; Deps = @{ "com.scaffold.maps" = "0.1.0"; "com.scaffold.records" = "0.1.0" } }
    "com.scaffold.model"        = @{ Display = "Scaffold MVVM Model"; Desc = "MVVM model layer."; Deps = @{ "com.scaffold.mvvm" = "0.1.0" } }
    "com.scaffold.entities"     = @{ Display = "Scaffold Entities"; Desc = "Gameplay entity building blocks."; Deps = @{} }
    "com.scaffold.viewmodel"    = @{ Display = "Scaffold ViewModel"; Desc = "MVVM view model layer."; Deps = @{ "com.scaffold.mvvm" = "0.1.0"; "com.scaffold.navigation" = "0.1.0" } }
    "com.scaffold.liveops"      = @{ Display = "Scaffold LiveOps"; Desc = "LiveOps services and DTOs."; Deps = @{ "com.scaffold.cloudcode" = "0.1.0"; "com.scaffold.scope" = "0.1.0" } }
    "com.scaffold.addressables" = @{ Display = "Scaffold Addressables"; Desc = "Addressables integration helpers."; Deps = @{ "com.scaffold.scope" = "0.1.0"; "com.scaffold.types" = "0.1.0"; "com.scaffold.maps" = "0.1.0"; "com.unity.addressables" = "2.9.1" } }
    "com.scaffold.navigation"   = @{
        Display = "Scaffold Navigation"
        Desc    = "View navigation and transitions."
        Deps    = @{
            "com.scaffold.events"       = "0.1.0"
            "com.scaffold.addressables" = "0.1.0"
            "com.scaffold.types"        = "0.1.0"
            "com.scaffold.schemas"      = $schemasGit
            "com.unity.addressables"    = "2.9.1"
        }
    }
    "com.scaffold.view"         = @{ Display = "Scaffold View"; Desc = "MVVM views and UI wiring."; Deps = @{ "com.scaffold.viewmodel" = "0.1.0"; "com.scaffold.mvvm" = "0.1.0"; "com.scaffold.navigation" = "0.1.0"; "com.scaffold.types" = "0.1.0"; "com.unity.ugui" = "2.0.0" } }
    "com.scaffold.bootstrap"    = @{
        Display = "Scaffold Bootstrap"
        Desc    = "Application bootstrap and composition root."
        Deps    = @{
            "com.scaffold.navigation"   = "0.1.0"
            "com.scaffold.schemas"      = $schemasGit
            "com.scaffold.scope"        = "0.1.0"
            "com.scaffold.mvvm"         = "0.1.0"
            "com.scaffold.view"         = "0.1.0"
            "com.scaffold.model"        = "0.1.0"
            "com.scaffold.viewmodel"    = "0.1.0"
            "com.scaffold.events"       = "0.1.0"
            "com.scaffold.addressables" = "0.1.0"
            "com.scaffold.ugs"          = "0.1.0"
            "com.scaffold.cloudcode"    = "0.1.0"
            "com.scaffold.liveops"      = "0.1.0"
            "com.scaffold.sceneflow"    = "0.1.0"
            "com.scaffold.types"        = "0.1.0"
            "jp.hadashikick.vcontainer"   = "1.17.0"
            "com.unity.addressables"      = "2.9.1"
            "com.unity.ugui"              = "2.0.0"
        }
    }
    "com.scaffold.autopacker" = @{ Display = "Scaffold AutoPacker"; Desc = "Source generator for record packing."; Deps = @{} }
}

foreach ($pkgId in ($packageSpecs.Keys | Sort-Object)) {
    $pkgDir = Join-Path $destRoot $pkgId
    if (-not (Test-Path $pkgDir)) { continue }
    $spec = $packageSpecs[$pkgId]
    $json = New-ScaffoldPackageJson -Name $pkgId -DisplayName $spec.Display -Description $spec.Desc -Dependencies $spec.Deps
    Set-Content -Path (Join-Path $pkgDir "package.json") -Value $json -Encoding UTF8 -NoNewline
    Write-Host "Wrote package.json for $pkgId"
}

Write-Host "Done."
