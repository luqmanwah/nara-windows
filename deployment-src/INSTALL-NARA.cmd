@echo off
setlocal
title Nara Installer
net session >nul 2>&1
if not "%ERRORLEVEL%"=="0" (
  powershell.exe -NoLogo -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Bootstrap\Install-Nara.ps1" -PackageRoot "%~dp0"
set "NARA_EXIT=%ERRORLEVEL%"
echo.
if not "%NARA_EXIT%"=="0" echo Nara berhenti dengan kode %NARA_EXIT%. Lihat folder Logs.
pause
exit /b %NARA_EXIT%
