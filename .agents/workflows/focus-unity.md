---
description: how to force Unity to compile code automatically without manual user intervention by focusing the application window
---

// turbo-all

# Focusing Unity Editor Instances
```powershell
Add-Type @"
  using System;
  using System.Runtime.InteropServices;
  public class Win32 {
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  }
"@
$unityProcesses = Get-Process -Name "Unity" -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -ne "" }
if ($unityProcesses) {
  foreach ($process in $unityProcesses) {
    [Win32]::ShowWindow($process.MainWindowHandle, 9)
    [Win32]::SetForegroundWindow($process.MainWindowHandle)
  }
  Write-Output "Brought $( $($unityProcesses).Count ) Unity instances to foreground."

  # Wait for compilation status
  Start-Sleep -Seconds 2
  $statusFile = "Temp\IsCompiling.txt"
  $maxWaitSeconds = 120
  $sw = [Diagnostics.Stopwatch]::StartNew()
  
  Write-Output "Checking compilation status..."
  while ($sw.Elapsed.TotalSeconds -lt $maxWaitSeconds) {
      $isCompiling = $false
      if (Test-Path $statusFile) {
          try {
              $stream = [System.IO.File]::Open($statusFile, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
              $reader = New-Object System.IO.StreamReader($stream)
              $status = $reader.ReadToEnd().Trim()
              $reader.Close()
              $stream.Close()
              if ($status -eq "true") { $isCompiling = $true }
          } catch { $isCompiling = $true }
      }
      if (-not $isCompiling) {
          Write-Output "Compilation is complete (or not compiling)."
          break
      }
      Start-Sleep -Milliseconds 500
  }
} else {
  Write-Output "No Unity processes found."
}
```

Wait for this command to complete.
