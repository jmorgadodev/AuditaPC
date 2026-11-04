@echo off
setlocal
start "Inventario TI" powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0Inventario-Equipo.ps1"
exit /b
