@echo off
setlocal
title Nara Plan Only
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Bootstrap\Install-Nara.ps1" -PackageRoot "%~dp0" -PlanOnly
set "NARA_EXIT=%ERRORLEVEL%"
echo.
pause
exit /b %NARA_EXIT%
