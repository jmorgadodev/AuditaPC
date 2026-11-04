@echo off
setlocal
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0Unificar-Inventarios.ps1"
exit /b %errorlevel%
