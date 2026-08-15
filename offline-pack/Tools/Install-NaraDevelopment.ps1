[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Nara-Registry.ps1')

$installRoot = Join-Path $env:ProgramData 'Nara'
$stateRoot = Join-Path $installRoot 'State'
$logRoot = Join-Path $installRoot 'Logs'
$backupPath = Join-Path $stateRoot 'registry-before.json'
$settings = @(
    [pscustomobject]@{ Path='HKCU:\Control Panel\Desktop\WindowMetrics'; Name='MinAnimate'; Value='0'; Kind='String' },
    [pscustomobject]@{ Path='HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'; Name='EnableTransparency'; Value=0; Kind='DWord' },
    [pscustomobject]@{ Path='HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects'; Name='VisualFXSetting'; Value=2; Kind='DWord' }
)

Write-Host ''
Write-Host 'NARA DEVELOPMENT CLIENT - PHASE 1' -ForegroundColor Cyan
Write-Host 'Animasi dan transparansi akan dimatikan untuk akun Windows ini.'
Write-Host 'Defender, Windows Update, driver, layanan inti, dan aplikasi tidak diubah.'
$answer = Read-Host 'Ketik INSTALL untuk melanjutkan'
if ($answer -cne 'INSTALL') { Write-Host 'Dibatalkan. Tidak ada perubahan.'; exit 2 }

New-Item -ItemType Directory -Path $stateRoot,$logRoot -Force | Out-Null
if (-not (Test-Path -LiteralPath $backupPath)) {
    Get-NaraRegistrySnapshot -Settings $settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $backupPath -Encoding UTF8
}
$collectorTarget = Join-Path $installRoot 'Collect-NaraHardware.ps1'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Collect-NaraHardware.ps1') -Destination $collectorTarget -Force

try {
    foreach ($setting in $settings) { Set-NaraRegistryValue -Setting $setting }
    $inventoryPath = Join-Path $logRoot ('hardware-{0}.json' -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
    & $collectorTarget -OutputPath $inventoryPath | Out-Null
    [ordered]@{ installedAtUtc=[DateTime]::UtcNow.ToString('o'); version='0.1.0-development'; scope='current-user'; status='installed' } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stateRoot 'installation.json') -Encoding UTF8
} catch {
    if (Test-Path -LiteralPath $backupPath) {
        Restore-NaraRegistrySnapshot -Snapshot @((Get-Content -LiteralPath $backupPath -Raw | ConvertFrom-Json))
    }
    throw
}
Write-Host 'Nara Phase 1 berhasil dipasang.' -ForegroundColor Green
Write-Host "Data dan log: $installRoot"
Write-Host 'Keluar lalu masuk kembali agar seluruh efek visual diterapkan.'
