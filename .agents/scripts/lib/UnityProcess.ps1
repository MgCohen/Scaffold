# Shared helper: Windows PowerShell 5.x Start-Process -ArgumentList breaks argv when paths contain spaces.
function Start-UnityEditorProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $parts = New-Object System.Collections.Generic.List[string]
    foreach ($a in $Arguments) {
        if ($null -eq $a) {
            continue
        }

        $s = [string]$a
        if ($s -match '[\s"]') {
            $parts.Add(('"' + ($s -replace '"', '""') + '"')) | Out-Null
        } else {
            $parts.Add($s) | Out-Null
        }
    }

    $argLine = [string]::Join(' ', $parts)
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $FilePath
    $psi.Arguments = $argLine
    $psi.UseShellExecute = $false
    $p = [System.Diagnostics.Process]::Start($psi)
    if ($null -eq $p) {
        throw "Failed to start Unity process."
    }

    return Get-Process -Id $p.Id
}
