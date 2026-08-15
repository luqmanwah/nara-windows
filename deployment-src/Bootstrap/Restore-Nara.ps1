[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$installRoot=Join-Path $env:ProgramData 'Nara'
$statePath=Join-Path $installRoot 'installation.json'
if (-not (Test-Path -LiteralPath $statePath)) { throw 'Instalasi Nara tidak ditemukan.' }
$state=Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
Import-Module (Join-Path $installRoot 'Core\Nara.Core.psm1') -Force
$snapshot=@(Get-Content -LiteralPath $state.backupPath -Raw | ConvertFrom-Json)
$approval=Read-Host 'Ketik RESTORE untuk memulihkan pengaturan sebelum Nara'
if ($approval -cne 'RESTORE') { Write-Host 'Dibatalkan.'; exit 2 }
Restore-NaraSettings -Snapshot $snapshot
[ordered]@{ restoredAtUtc=[DateTime]::UtcNow.ToString('o'); status='restored' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $installRoot 'restore.json') -Encoding UTF8
Write-Host 'Pengaturan sebelum Nara berhasil dipulihkan.' -ForegroundColor Green
