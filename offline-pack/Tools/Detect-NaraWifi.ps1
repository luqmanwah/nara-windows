[CmdletBinding()]
param([Parameter(Mandatory)] [string] $OutputPath)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$devices = @(Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object {
    $_.Class -eq 'Net' -or $_.FriendlyName -match 'Wireless|Wi-Fi|WLAN|Network Controller' -or $_.InstanceId -match '^PCI\\'
})

$results = @($devices | ForEach-Object {
    $hardwareIds = @()
    try {
        $property = Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName 'DEVPKEY_Device_HardwareIds' -ErrorAction Stop
        $hardwareIds = @($property.Data | ForEach-Object { [string] $_ })
    } catch {}
    if ($_.Class -eq 'Net' -or $_.FriendlyName -match 'Wireless|Wi-Fi|WLAN|Network Controller' -or $hardwareIds -match 'CC_0280') {
        [ordered]@{
            class = if ($null -eq $_.Class) { $null } else { [string] $_.Class }
            friendlyName = if ($null -eq $_.FriendlyName) { $null } else { [string] $_.FriendlyName }
            status = [string] $_.Status
            hardwareIds = $hardwareIds
        }
    }
})

$result = [ordered]@{
    schemaVersion = '1.0.0'
    collectedAtUtc = [DateTime]::UtcNow.ToString('o')
    deviceCount = $results.Count
    devices = $results
}
$parent = Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))
if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
$result | ConvertTo-Json -Depth 6
