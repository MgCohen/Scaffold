@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0validate-changes.ps1" %*
exit /b %ERRORLEVEL%
