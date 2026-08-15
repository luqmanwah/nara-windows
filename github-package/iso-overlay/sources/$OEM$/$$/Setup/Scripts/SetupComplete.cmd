@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "C:\NaraSetup\Remove-ConsumerApps.ps1" >> "C:\ProgramData\Nara\Logs\setupcomplete-console.log" 2>&1
exit /b 0
