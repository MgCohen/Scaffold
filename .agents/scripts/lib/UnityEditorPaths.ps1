# Resolve Unity Editor executable from project version, UNITY_PATH, or Unity Hub (Windows / macOS / Linux).
# Dot-source after UnityProcess.ps1 is optional (this file does not depend on it).

function Test-IsWindowsOs {
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        return $IsWindows
    }
    return $env:OS -match '(?i)Windows'
}

function Test-IsMacOs {
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        return $IsMacOS
    }
    try {
        return [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::OSX)
    } catch {
        return $false
    }
}

function Test-IsLinuxOs {
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        return $IsLinux
    }
    try {
        return [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::Linux)
    } catch {
        return $false
    }
}

function Get-UnityEditorVersionString {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedProjectPath
    )

    $versionFile = Join-Path $ResolvedProjectPath "ProjectSettings/ProjectVersion.txt"
    if (-not (Test-Path $versionFile)) {
        throw "Could not find ProjectVersion.txt at '$versionFile'."
    }

    $match = Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(.+)$'
    if (-not $match) {
        throw "Could not read m_EditorVersion from '$versionFile'."
    }

    return $match.Matches[0].Groups[1].Value.Trim()
}

function Get-UnityExecutableInVersionDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VersionDir
    )

    $candidates = @(
        (Join-Path $VersionDir "Editor/Unity.exe"),
        (Join-Path $VersionDir "Editor\Unity.exe"),
        (Join-Path $VersionDir "Unity.app/Contents/MacOS/Unity"),
        (Join-Path $VersionDir "Editor/Unity")
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c) {
            return (Resolve-Path -LiteralPath $c).Path
        }
    }
    return $null
}

function Get-UnityHubEditorRootDirectories {
    $roots = [System.Collections.Generic.List[string]]::new()
    if (Test-IsWindowsOs) {
        foreach ($hub in @(
            (Join-Path $env:ProgramFiles "Unity/Hub/Editor"),
            (Join-Path ${env:ProgramFiles(x86)} "Unity/Hub/Editor")
        )) {
            if ($hub -and (Test-Path -LiteralPath $hub)) {
                $roots.Add($hub) | Out-Null
            }
        }
    } elseif (Test-IsMacOs) {
        $macHub = "/Applications/Unity/Hub/Editor"
        if (Test-Path -LiteralPath $macHub) {
            $roots.Add($macHub) | Out-Null
        }
    } elseif (Test-IsLinuxOs) {
        $home = [Environment]::GetFolderPath("UserProfile")
        $linuxHub = Join-Path $home "Unity/Hub/Editor"
        if (Test-Path -LiteralPath $linuxHub) {
            $roots.Add($linuxHub) | Out-Null
        }
    }
    return ,$roots.ToArray()
}

function Get-InstalledUnityEditorExecutables {
    $result = [System.Collections.Generic.List[object]]::new()
    foreach ($hubRoot in (Get-UnityHubEditorRootDirectories)) {
        foreach ($dir in (Get-ChildItem -Path $hubRoot -Directory -ErrorAction SilentlyContinue)) {
            $exe = Get-UnityExecutableInVersionDirectory -VersionDir $dir.FullName
            if ([string]::IsNullOrWhiteSpace($exe)) {
                continue
            }

            $name = $dir.Name
            $rank = $null
            if ($name -match '^(\d+)\.(\d+)\.(\d+)([a-z])(\d+)$') {
                $channel = $matches[4]
                $channelRank = switch ($channel) {
                    "a" { 1 }
                    "b" { 2 }
                    "f" { 3 }
                    "p" { 4 }
                    default { 0 }
                }

                $rank = [pscustomobject]@{
                    Major = [int]$matches[1]
                    Minor = [int]$matches[2]
                    Patch = [int]$matches[3]
                    Channel = $channelRank
                    Build = [int]$matches[5]
                }
            } else {
                $rank = [pscustomobject]@{
                    Major = -1; Minor = -1; Patch = -1; Channel = -1; Build = -1
                }
            }

            $result.Add([pscustomobject]@{
                VersionLabel = $name
                UnityExe = $exe
                Rank = $rank
            }) | Out-Null
        }
    }
    return ,$result.ToArray()
}

function Resolve-UnityEditorPath {
    <#
    .SYNOPSIS
        Resolve path to Unity Editor binary.
    .PARAMETER Strict
        If set, only exact Hub version match (or UNITY_PATH / -UnityPath). Fallback to newest Hub install
        requires env SCAFFOLD_UNITY_ALLOW_VERSION_FALLBACK=1.
    .PARAMETER AllowHubVersionFallback
        When not -Strict: if true (default), pick newest Hub editor when exact version is missing.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedProjectPath,
        [string]$RequestedUnityPath,
        [switch]$Strict,
        [bool]$AllowHubVersionFallback = $true,
        [string]$ExampleScriptHint = ".agents/scripts/run-unity-tests.ps1"
    )

    $envFallback = $env:SCAFFOLD_UNITY_ALLOW_VERSION_FALLBACK
    if ($Strict) {
        $allowFallback = ($envFallback -match '^(1|true|yes)$')
    } else {
        $allowFallback = $AllowHubVersionFallback -or ($envFallback -match '^(1|true|yes)$')
    }

    $attempted = @()

    if ($RequestedUnityPath) {
        $attempted += ("-UnityPath: {0}" -f $RequestedUnityPath)
        if (-not (Test-Path -LiteralPath $RequestedUnityPath)) {
            throw "Unity executable was not found at '$RequestedUnityPath'."
        }

        return (Resolve-Path -LiteralPath $RequestedUnityPath).Path
    }

    if ($env:UNITY_PATH) {
        $attempted += ("UNITY_PATH: {0}" -f $env:UNITY_PATH)
        if (-not (Test-Path -LiteralPath $env:UNITY_PATH)) {
            throw ("UNITY_PATH points to a missing file: '{0}'." -f $env:UNITY_PATH)
        }

        return (Resolve-Path -LiteralPath $env:UNITY_PATH).Path
    }

    $version = Get-UnityEditorVersionString -ResolvedProjectPath $ResolvedProjectPath
    foreach ($hubRoot in (Get-UnityHubEditorRootDirectories)) {
        $exactDir = Join-Path $hubRoot $version
        if (Test-Path -LiteralPath $exactDir) {
            $exe = Get-UnityExecutableInVersionDirectory -VersionDir $exactDir
            if (-not [string]::IsNullOrWhiteSpace($exe)) {
                return $exe
            }
        }
        $attempted += ("Hub: {0}/{1}" -f $hubRoot, $version)
    }

    $installed = @(Get-InstalledUnityEditorExecutables)
    if ($allowFallback -and $installed.Count -gt 0) {
        $fallback = $installed | Sort-Object `
            @{ Expression = { $_.Rank.Major }; Descending = $true }, `
            @{ Expression = { $_.Rank.Minor }; Descending = $true }, `
            @{ Expression = { $_.Rank.Patch }; Descending = $true }, `
            @{ Expression = { $_.Rank.Channel }; Descending = $true }, `
            @{ Expression = { $_.Rank.Build }; Descending = $true }, `
            @{ Expression = { $_.VersionLabel }; Descending = $true } | Select-Object -First 1

        Write-Host ("Requested Unity version '{0}' was not found. Falling back to installed '{1}'." -f $version, $fallback.VersionLabel)
        return $fallback.UnityExe
    }

    $attemptSummary = $attempted | ForEach-Object { "  - $_" }
    $examplePath = if (Test-IsMacOs) { "<path-to-Unity.app/Contents/MacOS/Unity>" } else { "<path-to-Unity.exe>" }
    $exampleCommand = "pwsh -File `"$ExampleScriptHint`" -UnityPath `"$examplePath`""
    $message = @(
        ("Unity executable could not be resolved for version '{0}'." -f $version),
        "Attempted locations:",
        ($attemptSummary -join [Environment]::NewLine),
        "Set UNITY_PATH or pass -UnityPath, for example:",
        ("  {0}" -f $exampleCommand)
    ) -join [Environment]::NewLine
    throw $message
}

function Get-UnityTestFailureMessage {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlNode]$Node
    )

    $failureNode = $Node.SelectSingleNode("failure/message")
    if (-not $failureNode) {
        return ""
    }

    return ($failureNode.InnerText -replace '\s+', ' ').Trim()
}
