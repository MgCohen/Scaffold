@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-coverage-audit.ps1" %*
exit /b %ERRORLEVEL%
