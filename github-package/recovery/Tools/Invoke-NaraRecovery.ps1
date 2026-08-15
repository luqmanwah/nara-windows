[CmdletBinding()]
param([Parameter(Mandatory)][string]$RecoveryRoot)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$installRoot = Join-Path $env:ProgramData 'Nara'
$statePath = Join-Path $installRoot 'installation.json'
if (-not (Test-Path -LiteralPath $statePath)) { throw 'No Nara installation state was found. Nothing was changed.' }
$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
if (-not (Test-Path -LiteralPath $state.backupPath)) { throw "The recorded rollback snapshot is missing: $($state.backupPath)" }
$module = Join-Path $installRoot 'Core\Nara.Core.psm1'
if (-not (Test-Path -LiteralPath $module)) { $module = Join-Path $RecoveryRoot 'Core\Nara.Core.psm1' }
if (-not (Test-Path -LiteralPath $module)) { throw 'Nara recovery core is missing.' }
Import-Module $module -Force
$snapshot = @(Get-Content -LiteralPath $state.backupPath -Raw | ConvertFrom-Json)
Write-Host "Rollback snapshot: $($state.backupPath)" -ForegroundColor Yellow
$approval = Read-Host 'Type RESTORE to restore the pre-Nara settings'
if ($approval -cne 'RESTORE') { Write-Host 'Cancelled. Nothing was changed.'; exit 2 }
Restore-NaraSettings -Snapshot $snapshot
$report = [ordered]@{ schemaVersion='1.0.0'; restoredAtUtc=[DateTime]::UtcNow.ToString('o'); backupPath=$state.backupPath; status='restored' }
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $installRoot 'recovery-report.json') -Encoding UTF8
Write-Host 'Pre-Nara settings restored successfully.' -ForegroundColor Green
