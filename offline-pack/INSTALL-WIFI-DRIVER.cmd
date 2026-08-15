@echo off
setlocal
title Nara Wi-Fi Driver Installer
net session >nul 2>&1
if not "%ERRORLEVEL%"=="0" (
  powershell.exe -NoLogo -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
if not exist "%~dp0Drivers\WiFi-RTL8723BE" (
  echo Paket driver belum tersedia. Jalankan EXPORT-WIFI-DRIVER.cmd pada Acer sumber.
  pause
  exit /b 2
)
pnputil.exe /add-driver "%~dp0Drivers\WiFi-RTL8723BE\*.inf" /subdirs /install > "%~dp0Logs\wifi-driver-install.txt" 2>&1
set "NARA_EXIT=%ERRORLEVEL%"
type "%~dp0Logs\wifi-driver-install.txt"
echo.
if "%NARA_EXIT%"=="0" (echo Instalasi driver selesai.) else (echo Instalasi gagal. Lihat log.)
pause
exit /b %NARA_EXIT%
