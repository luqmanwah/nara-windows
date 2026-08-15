@echo off
setlocal
cd /d "%~dp0"

if not exist "Logs" mkdir "Logs"

echo Nara Audit Only
echo Tidak ada pengaturan Windows yang akan diubah.
echo.

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Collect-NaraHardware.ps1" -OutputPath "%~dp0Logs\hardware-inventory.json" > "%~dp0Logs\audit-console.txt" 2>&1

if errorlevel 1 (
    echo Audit gagal. Buka Logs\audit-console.txt untuk melihat detail.
    pause
    exit /b 1
)

echo Audit selesai.
echo Hasil: Logs\hardware-inventory.json
pause

