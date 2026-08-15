Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-NaraTargetInventory {
    $current = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
    $cs = Get-CimInstance Win32_ComputerSystem
    $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
    $gpu = @(Get-CimInstance Win32_VideoController | ForEach-Object {
        [ordered]@{ name=[string]$_.Name; driverVersion=[string]$_.DriverVersion; adapterMemoryBytes=[long]$_.AdapterRAM }
    })
    [ordered]@{
        collectedAtUtc = [DateTime]::UtcNow.ToString('o')
        manufacturer = [string]$cs.Manufacturer
        model = [string]$cs.Model
        windows = [ordered]@{ productName=[string]$current.ProductName; edition=[string]$current.EditionID; build=[string]$current.CurrentBuild; ubr=[int]$current.UBR; architecture=$env:PROCESSOR_ARCHITECTURE }
        cpu = [ordered]@{ name=[string]$cpu.Name; cores=[int]$cpu.NumberOfCores; logicalProcessors=[int]$cpu.NumberOfLogicalProcessors }
        memory = [ordered]@{ installedBytes=[long]$cs.TotalPhysicalMemory }
        graphics = $gpu
    }
}

function Resolve-NaraProfile {
    param([Parameter(Mandatory)] $Inventory)
    $ramGiB = [math]::Floor([double]$Inventory.memory.installedBytes / 1GB)
    $maxVramGiB = 0
    foreach ($gpu in $Inventory.graphics) { $maxVramGiB = [math]::Max($maxVramGiB, [double]$gpu.adapterMemoryBytes / 1GB) }
    if ($ramGiB -lt 12) { return [ordered]@{ id='ultra-lite'; localModel='none'; coreMode='on-demand' } }
    if ($ramGiB -ge 24 -and $maxVramGiB -lt 8) { return [ordered]@{ id='ai-workstation-lite'; localModel='benchmark-required'; coreMode='on-demand-with-idle-unload' } }
    if ($ramGiB -ge 24) { return [ordered]@{ id='ai-workstation'; localModel='benchmark-required'; coreMode='on-demand' } }
    return [ordered]@{ id='balanced'; localModel='optional-after-benchmark'; coreMode='on-demand' }
}

function Get-NaraSettings {
    @(
        [pscustomobject]@{ id='NARA-UI-001'; key='ui.animations'; path='HKCU:\Control Panel\Desktop\WindowMetrics'; name='MinAnimate'; kind='String'; desired='0'; risk='low' },
        [pscustomobject]@{ id='NARA-UI-002'; key='ui.transparency'; path='HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'; name='EnableTransparency'; kind='DWord'; desired=0; risk='low' },
        [pscustomobject]@{ id='NARA-UI-003'; key='ui.visualEffects'; path='HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects'; name='VisualFXSetting'; kind='DWord'; desired=2; risk='low' }
    )
}

function Get-NaraRegistrySnapshot {
    param([Parameter(Mandatory)] [array]$Settings)
    @($Settings | ForEach-Object {
        $exists=$false; $value=$null; $kind=$null
        if (Test-Path -LiteralPath $_.path) {
            $key=Get-Item -LiteralPath $_.path
            $value=$key.GetValue($_.name,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
            if ($null -ne $value) { $exists=$true; $kind=[string]$key.GetValueKind($_.name) }
        }
        [ordered]@{ id=$_.id; path=$_.path; name=$_.name; existed=$exists; kind=$kind; value=$value }
    })
}

function Set-NaraSettings {
    param([Parameter(Mandatory)] [array]$Settings)
    foreach ($setting in $Settings) {
        if (-not (Test-Path -LiteralPath $setting.path)) { New-Item -Path $setting.path -Force | Out-Null }
        New-ItemProperty -LiteralPath $setting.path -Name $setting.name -Value $setting.desired -PropertyType $setting.kind -Force | Out-Null
        $actual=(Get-Item -LiteralPath $setting.path).GetValue($setting.name)
        if ([string]$actual -ne [string]$setting.desired) { throw "Verification failed: $($setting.id)" }
    }
}

function Restore-NaraSettings {
    param([Parameter(Mandatory)] [array]$Snapshot)
    foreach ($item in $Snapshot) {
        if ($item.existed) {
            if (-not (Test-Path -LiteralPath $item.path)) { New-Item -Path $item.path -Force | Out-Null }
            New-ItemProperty -LiteralPath $item.path -Name $item.name -Value $item.value -PropertyType $item.kind -Force | Out-Null
        } elseif (Test-Path -LiteralPath $item.path) {
            Remove-ItemProperty -LiteralPath $item.path -Name $item.name -ErrorAction SilentlyContinue
        }
    }
}

function Write-NaraJson {
    param([Parameter(Mandatory)]$Value,[Parameter(Mandatory)][string]$Path)
    $parent=Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    $Value | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Path -Encoding UTF8
}

Export-ModuleMember -Function Get-NaraTargetInventory,Resolve-NaraProfile,Get-NaraSettings,Get-NaraRegistrySnapshot,Set-NaraSettings,Restore-NaraSettings,Write-NaraJson
