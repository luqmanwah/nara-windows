[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$setupRoot='C:\NaraSetup'
$logRoot='C:\ProgramData\Nara\Logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$logPath=Join-Path $logRoot 'unattend-consumer-apps.log'
$catalog=Get-Content -LiteralPath (Join-Path $setupRoot 'consumer-apps.json') -Raw | ConvertFrom-Json

function Write-NaraSetupLog([string]$Message) {
    ('{0} {1}' -f [DateTime]::UtcNow.ToString('o'),$Message) | Add-Content -LiteralPath $logPath -Encoding UTF8
}

Write-NaraSetupLog 'START conservative AppX cleanup'
$provisioned=@(Get-AppxProvisionedPackage -Online)
foreach($pattern in $catalog.packageNamePatterns) {
    $matches=@($provisioned | Where-Object DisplayName -Like $pattern)
    foreach($package in $matches) {
        try {
            Remove-AppxProvisionedPackage -Online -PackageName $package.PackageName -AllUsers -ErrorAction Stop | Out-Null
            Write-NaraSetupLog "REMOVED provisioned $($package.DisplayName)"
        } catch {
            Write-NaraSetupLog "SKIPPED $($package.DisplayName): $($_.Exception.Message)"
        }
    }
}
Write-NaraSetupLog 'PROTECTED Defender WindowsUpdate servicing recovery drivers StoreInfrastructure runtimes'
Write-NaraSetupLog 'FINISH conservative AppX cleanup'
