[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Nara-Registry.ps1')

$stateRoot = Join-Path (Join-Path $env:ProgramData 'Nara') 'State'
$backupPath = Join-Path $stateRoot 'registry-before.json'
if (-not (Test-Path -LiteralPath $backupPath)) { Write-Host 'Backup Nara tidak ditemukan.'; exit 1 }
$answer = Read-Host 'Ketik RESTORE untuk mengembalikan pengaturan sebelum Nara'
if ($answer -cne 'RESTORE') { Write-Host 'Dibatalkan.'; exit 2 }
Restore-NaraRegistrySnapshot -Snapshot @((Get-Content -LiteralPath $backupPath -Raw | ConvertFrom-Json))
[ordered]@{ restoredAtUtc=[DateTime]::UtcNow.ToString('o'); status='restored' } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stateRoot 'restore.json') -Encoding UTF8
Write-Host 'Pengaturan awal berhasil dipulihkan.' -ForegroundColor Green
Write-Host 'Keluar lalu masuk kembali agar seluruh efek visual diterapkan.'
