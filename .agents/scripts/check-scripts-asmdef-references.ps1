[CmdletBinding()]
param(
    [string]$ProjectPath = (Get-Location).Path,
    [string[]]$ScriptsRoots = @("Assets/Scripts", "Packages"),
    [string[]]$ExcludedAssemblyNames,
    [string[]]$ExcludedGuidReferences
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $PSCommandPath
. (Join-Path (Split-Path $scriptDirectory -Parent) (Join-Path "testing" "TestingSuite.Config.ps1"))

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path
$testingSuiteConfig = Get-TestingSuiteConfig -ProjectPath $resolvedProjectPath
if (-not $ExcludedAssemblyNames -or $ExcludedAssemblyNames.Count -eq 0) {
    $ExcludedAssemblyNames = $testingSuiteConfig.AsmdefExcludedAssemblyNames
}
if (-not $ExcludedGuidReferences -or $ExcludedGuidReferences.Count -eq 0) {
    $ExcludedGuidReferences = $testingSuiteConfig.AsmdefExcludedGuidReferences
}
$resolvedScriptsRoots = @()
foreach ($root in $ScriptsRoots) {
    $resolved = Join-Path $resolvedProjectPath $root
    if (Test-Path $resolved) {
        $resolvedScriptsRoots += $resolved
    }
}

# First-party UPM-style trees under Assets/Packages (e.g. com.scaffold.types) — scan each com.scaffold.* folder only (skip third-party asset packs in the same parent).
$assetsPackagesRoot = Join-Path $resolvedProjectPath "Assets/Packages"
if (Test-Path $assetsPackagesRoot) {
    $scaffoldPackageDirs = @(Get-ChildItem -Path $assetsPackagesRoot -Directory -Filter "com.scaffold.*" -ErrorAction SilentlyContinue)
    foreach ($dir in $scaffoldPackageDirs) {
        $resolvedScriptsRoots += $dir.FullName
    }
}

if ($resolvedScriptsRoots.Count -eq 0) {
    Write-Output ("SCRIPTS_ROOTS_NOT_FOUND:{0}" -f ($ScriptsRoots -join ";"))
    Write-Output "TOTAL:0"
    exit 0
}

$asmdefs = @()
foreach ($resolvedScriptsRoot in $resolvedScriptsRoots) {
    $asmdefs += @(Get-ChildItem -Path $resolvedScriptsRoot -Recurse -File -Filter *.asmdef | Sort-Object -Property FullName)
}
$asmdefs = @($asmdefs | Sort-Object -Property FullName)

$nameToPath = @{}
$guidToPath = @{}
$issues = New-Object System.Collections.Generic.List[object]

foreach ($asmdef in $asmdefs) {
    $json = Get-Content $asmdef.FullName -Raw | ConvertFrom-Json
    if (-not [string]::IsNullOrWhiteSpace($json.name)) {
        $nameToPath[$json.name] = $asmdef.FullName
    }

    $metaPath = "$($asmdef.FullName).meta"
    if (-not (Test-Path $metaPath)) { continue }

    $guidLine = Get-Content $metaPath | Where-Object { $_ -match "^guid:\s*" } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($guidLine)) { continue }

    $guid = $guidLine -replace "^guid:\s*", ""
    if (-not [string]::IsNullOrWhiteSpace($guid)) {
        $guidToPath[$guid] = $asmdef.FullName
    }
}

foreach ($asmdef in $asmdefs) {
    $json = Get-Content $asmdef.FullName -Raw | ConvertFrom-Json
    $references = @()
    $refProp = $json.PSObject.Properties['references']
    if ($null -ne $refProp -and $null -ne $refProp.Value) {
        $references = @($json.references)
    }

    foreach ($reference in $references) {
        if ($null -eq $reference) {
            $issues.Add([pscustomobject]@{
                    Type     = "NullReferenceEntry"
                    Assembly = $asmdef.FullName
                    Reference = "null"
                    Detail   = "Reference entry is null."
                })
            continue
        }

        $referenceValue = [string]$reference
        if ($ExcludedGuidReferences -contains $referenceValue) { continue }

        if ($referenceValue -match "^GUID:") {
            $guid = $referenceValue.Substring(5)
            if (-not $guidToPath.ContainsKey($guid)) {
                $issues.Add([pscustomobject]@{
                        Type      = "MissingScriptsGuidReference"
                        Assembly  = $asmdef.FullName
                        Reference = $referenceValue
                        Detail    = "GUID does not match an asmdef under Assets/Scripts, Assets/Packages/com.scaffold.*, or Packages."
                    })
            }

            continue
        }

        if ($ExcludedAssemblyNames -contains $referenceValue) { continue }

        $isProjectAssemblyName = $false
        foreach ($pattern in $testingSuiteConfig.AsmdefFirstPartyAssemblyNamePatterns) {
            if ($referenceValue -like $pattern) {
                $isProjectAssemblyName = $true
                break
            }
        }
        if ($isProjectAssemblyName -and -not $nameToPath.ContainsKey($referenceValue)) {
            $issues.Add([pscustomobject]@{
                    Type      = "MissingScriptsAssemblyName"
                    Assembly  = $asmdef.FullName
                    Reference = $referenceValue
                    Detail    = "Assembly name does not match any asmdef under Assets/Scripts, Assets/Packages/com.scaffold.*, or Packages."
                })
        }
    }
}

$sortedIssues = @($issues | Sort-Object -Property Type, Assembly, Reference)
Write-Output ("SCRIPTS_ASMDEF_COUNT:{0}" -f $asmdefs.Count)
Write-Output ("TOTAL:{0}" -f $sortedIssues.Count)

foreach ($issue in $sortedIssues) {
    Write-Output ("ISSUE:{0}|{1}|{2}|{3}" -f $issue.Type, $issue.Assembly, $issue.Reference, $issue.Detail)
}

if ($sortedIssues.Count -gt 0) {
    exit 1
}

exit 0
