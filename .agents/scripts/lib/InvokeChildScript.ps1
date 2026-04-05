# Call a child PowerShell script in-process so $LASTEXITCODE and paths with spaces are preserved (Windows PowerShell 5.x).
function Invoke-ChildPowerShellScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath,
        [hashtable]$Parameters
    )

    $boundParams = @{}
    if ($Parameters) {
        foreach ($entry in $Parameters.GetEnumerator()) {
            if ($null -eq $entry.Value) {
                continue
            }

            if ($entry.Value -is [array]) {
                $boundParams[$entry.Key] = $entry.Value
            } else {
                $boundParams[$entry.Key] = $entry.Value
            }
        }
    }

    $rawOutput = & $ScriptPath @boundParams 2>&1
    $exit = if (Test-Path variable:LASTEXITCODE) { [int]$LASTEXITCODE } else { 0 }
    $output = @($rawOutput | ForEach-Object { "$_" })

    return [pscustomobject]@{
        ExitCode = $exit
        Output = $output
    }
}
