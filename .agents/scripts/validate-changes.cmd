@echo off
REM PowerShell 5.x: use ";" not "&&" to chain; or use cmd /c "cd /d <repo> && …" — see Docs/Testing.md
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0validate-changes.ps1" %*
exit /b %ERRORLEVEL%
