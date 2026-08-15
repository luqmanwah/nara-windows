@echo off
setlocal
title Nara Wi-Fi Detector
if not exist "%~dp0Logs" mkdir "%~dp0Logs"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Detect-NaraWifi.ps1" -OutputPath "%~dp0Logs\wifi-hardware.json" > "%~dp0Logs\wifi-console.txt" 2>&1
set "NARA_EXIT=%ERRORLEVEL%"
type "%~dp0Logs\wifi-console.txt"
echo.
if "%NARA_EXIT%"=="0" (
  echo Selesai. Hasil: Logs\wifi-hardware.json
) else (
  echo Deteksi gagal. Exit code: %NARA_EXIT%
)
pause
exit /b %NARA_EXIT%
