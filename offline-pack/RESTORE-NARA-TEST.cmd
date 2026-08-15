@echo off
setlocal
title Nara Development Client Restore
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Restore-NaraDevelopment.ps1"
set "NARA_EXIT=%ERRORLEVEL%"
echo.
if not "%NARA_EXIT%"=="0" echo Restore did not complete. Exit code: %NARA_EXIT%
pause
exit /b %NARA_EXIT%
