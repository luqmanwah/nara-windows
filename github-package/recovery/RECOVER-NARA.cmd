@echo off
setlocal
title Nara Recovery
net session >nul 2>&1
if not "%ERRORLEVEL%"=="0" (
  powershell.exe -NoLogo -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Invoke-NaraRecovery.ps1" -RecoveryRoot "%~dp0"
set "NARA_EXIT=%ERRORLEVEL%"
echo.
pause
exit /b %NARA_EXIT%
