@echo off
setlocal
title Nara Restore
net session >nul 2>&1
if not "%ERRORLEVEL%"=="0" (
  powershell.exe -NoLogo -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Bootstrap\Restore-Nara.ps1"
pause
