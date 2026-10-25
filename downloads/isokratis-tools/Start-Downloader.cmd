@echo off
chcp 65001 >nul
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Download-Isokratis-Voices.ps1" -OpenFolder
echo.
pause
