[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageRoot,
    [switch]$PlanOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PackageRoot 'Core\Nara.Core.psm1') -Force

$logRoot=Join-Path $PackageRoot 'Logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$inventory=Get-NaraTargetInventory
$profile=Resolve-NaraProfile -Inventory $inventory
$settings=@(Get-NaraSettings)
$plan=[ordered]@{
    schemaVersion='1.0.0'; generatedAtUtc=[DateTime]::UtcNow.ToString('o'); status='proposed'
    target=[ordered]@{ manufacturer=$inventory.manufacturer; model=$inventory.model; build=$inventory.windows.build }
    profile=$profile
    actions=@($settings | ForEach-Object { [ordered]@{ id=$_.id; setting=$_.key; risk=$_.risk; desired=$_.desired; rollback='restore captured registry value' } })
    protected=@('Defender','Windows Update','servicing','recovery','drivers','activation','networking')
}
$planPath=Join-Path $logRoot 'proposed-plan.json'
Write-NaraJson -Value $inventory -Path (Join-Path $logRoot 'hardware-inventory.json')
Write-NaraJson -Value $plan -Path $planPath
$planHash=(Get-FileHash -LiteralPath $planPath -Algorithm SHA256).Hash
Write-Host "Target : $($inventory.manufacturer) $($inventory.model)"
Write-Host "Profile: $($profile.id)"
Write-Host "Plan   : $planPath"
Write-Host "SHA256 : $planHash"
if ($PlanOnly) { Write-Host 'PLAN ONLY: Windows tidak diubah.' -ForegroundColor Cyan; exit 0 }

Write-Host ''
Write-Host 'Bundle tindakan:' -ForegroundColor Yellow
$settings | ForEach-Object { Write-Host "- $($_.id): $($_.key) -> $($_.desired)" }
$approval=Read-Host 'Ketik APPLY diikuti 8 karakter awal SHA256, contoh APPLY ABCD1234'
if ($approval -cne ('APPLY '+$planHash.Substring(0,8))) { Write-Host 'Persetujuan tidak cocok. Tidak ada perubahan.'; exit 2 }

$installRoot=Join-Path $env:ProgramData 'Nara'
$backupPath=Join-Path $installRoot ('Backups\registry-{0}.json' -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
$snapshot=@(Get-NaraRegistrySnapshot -Settings $settings)
Write-NaraJson -Value $snapshot -Path $backupPath
try {
    Set-NaraSettings -Settings $settings
    New-Item -ItemType Directory -Path (Join-Path $installRoot 'Core') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PackageRoot 'Core\Nara.Core.psm1') -Destination (Join-Path $installRoot 'Core\Nara.Core.psm1') -Force
    [ordered]@{ installedAtUtc=[DateTime]::UtcNow.ToString('o'); profile=$profile; planSha256=$planHash; backupPath=$backupPath; status='installed' } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $installRoot 'installation.json') -Encoding UTF8
} catch {
    Restore-NaraSettings -Snapshot $snapshot
    throw
}
Write-Host 'Nara Core tahap pertama berhasil dipasang dan diverifikasi.' -ForegroundColor Green
