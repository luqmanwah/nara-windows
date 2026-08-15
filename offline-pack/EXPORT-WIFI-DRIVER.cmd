@echo off
setlocal
title Nara Wi-Fi Driver Export
net session >nul 2>&1
if not "%ERRORLEVEL%"=="0" (
  powershell.exe -NoLogo -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Export-NaraWifiDriver.ps1" -Destination "%~dp0Drivers\WiFi-RTL8723BE" -LogPath "%~dp0Logs\wifi-driver-export.txt"
set "NARA_EXIT=%ERRORLEVEL%"
echo.
if "%NARA_EXIT%"=="0" (echo Driver Wi-Fi berhasil disimpan.) else (echo Ekspor gagal. Lihat Logs\wifi-driver-export.txt)
pause
exit /b %NARA_EXIT%
