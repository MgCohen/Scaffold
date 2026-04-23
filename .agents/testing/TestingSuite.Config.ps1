# Shared loader for `.agents/testing/TestingSuite.config.json`. Dot-source from `.agents/scripts/*.ps1` and `lib/`.
# If the JSON file is missing, returns the same defaults the repo used before config existed.

function Get-TestingSuiteConfig {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    $resolvedRoot = (Resolve-Path $ProjectPath).Path
    $configPath = Join-Path $resolvedRoot ".agents\testing\TestingSuite.config.json"

    $coverageDefaultAssemblyFilters = "+Scaffold.*"
    $excludedAssemblyNames = @(
        "Scaffold.Schemas",
        "Scaffold.LiveOps.Core.DTO.dll",
        "Scaffold.LiveOps.Modules.DTO.dll"
    )
    $excludedGuidReferences = @(
        "GUID:c72c3642ec330a340ad91bd6bf5d6bdc"
    )
    $firstPartyAssemblyNamePatterns = @(
        "Scaffold.*"
    )

    if (Test-Path $configPath) {
        $json = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($json.coverage -and $json.coverage.defaultAssemblyFilters) {
            $coverageDefaultAssemblyFilters = [string]$json.coverage.defaultAssemblyFilters
        }
        if ($json.asmdefReferences) {
            $ar = $json.asmdefReferences
            if ($ar.excludedAssemblyNames) {
                $excludedAssemblyNames = @($ar.excludedAssemblyNames)
            }
            if ($ar.excludedGuidReferences) {
                $excludedGuidReferences = @($ar.excludedGuidReferences)
            }
            if ($ar.firstPartyAssemblyNamePatterns) {
                $firstPartyAssemblyNamePatterns = @($ar.firstPartyAssemblyNamePatterns)
            }
        }
    }

    return [pscustomobject]@{
        CoverageDefaultAssemblyFilters = $coverageDefaultAssemblyFilters
        AsmdefExcludedAssemblyNames      = $excludedAssemblyNames
        AsmdefExcludedGuidReferences     = $excludedGuidReferences
        AsmdefFirstPartyAssemblyNamePatterns = $firstPartyAssemblyNamePatterns
    }
}
