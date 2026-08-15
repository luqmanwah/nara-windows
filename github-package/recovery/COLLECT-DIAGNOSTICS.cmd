@echo off
setlocal
title Nara Recovery Diagnostics
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Collect-NaraRecoveryDiagnostics.ps1" -OutputRoot "%~dp0Diagnostics"
echo.
pause
