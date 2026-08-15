[CmdletBinding()]
param(
    [Parameter()]
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$warnings = [System.Collections.Generic.List[string]]::new()
$conflicts = [System.Collections.Generic.List[string]]::new()
$sourceStatus = [ordered]@{
    registry  = 'unavailable'
    nativeApi = 'unavailable'
    cim       = 'unavailable'
}

function Get-OptionalRegistryValue {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $Name
    )

    try {
        return Get-ItemPropertyValue -LiteralPath $Path -Name $Name -ErrorAction Stop
    }
    catch {
        return $null
    }
}

function Convert-ToNullableInt64 {
    param([object] $Value)

    if ($null -eq $Value) {
        return $null
    }

    try {
        return [long] $Value
    }
    catch {
        return $null
    }
}

$isAdministrator = $false
try {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    $isAdministrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}
catch {
    $warnings.Add('Administrator status could not be determined.')
}

$currentVersionPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
$biosPath = 'HKLM:\HARDWARE\DESCRIPTION\System\BIOS'
$cpuPath = 'HKLM:\HARDWARE\DESCRIPTION\System\CentralProcessor\0'

$productName = Get-OptionalRegistryValue -Path $currentVersionPath -Name 'ProductName'
$editionId = Get-OptionalRegistryValue -Path $currentVersionPath -Name 'EditionID'
$displayVersion = Get-OptionalRegistryValue -Path $currentVersionPath -Name 'DisplayVersion'
$build = Get-OptionalRegistryValue -Path $currentVersionPath -Name 'CurrentBuild'
$ubrRaw = Get-OptionalRegistryValue -Path $currentVersionPath -Name 'UBR'
$cpuName = Get-OptionalRegistryValue -Path $cpuPath -Name 'ProcessorNameString'
$manufacturer = Get-OptionalRegistryValue -Path $biosPath -Name 'SystemManufacturer'
$model = Get-OptionalRegistryValue -Path $biosPath -Name 'SystemProductName'
$biosVersion = Get-OptionalRegistryValue -Path $biosPath -Name 'BIOSVersion'
$biosReleaseDate = Get-OptionalRegistryValue -Path $biosPath -Name 'BIOSReleaseDate'

if ($null -ne $productName -or $null -ne $build -or $null -ne $cpuName) {
    $sourceStatus.registry = 'success'
}

$buildNumber = 0
if ($null -ne $build -and [int]::TryParse([string] $build, [ref] $buildNumber)) {
    if ($buildNumber -ge 22000 -and [string] $productName -match 'Windows 10') {
        $conflicts.Add('Registry product name conflicts with the detected build family.')
    }
}

$physicallyInstalledBytes = $null
$usableTotalBytes = $null
$availableBytes = $null

if (-not ([System.Management.Automation.PSTypeName] 'Nara.Native.Memory').Type) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace Nara.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    public static class Memory
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx status);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);
    }
}
'@
}

try {
    [ulong] $installedKilobytes = 0
    $installedOk = [Nara.Native.Memory]::GetPhysicallyInstalledSystemMemory([ref] $installedKilobytes)

    $memoryStatus = [Nara.Native.MemoryStatusEx]::new()
    $statusOk = [Nara.Native.Memory]::GlobalMemoryStatusEx($memoryStatus)

    if ($installedOk) {
        $physicallyInstalledBytes = [long] ($installedKilobytes * 1KB)
    }

    if ($statusOk) {
        $usableTotalBytes = [long] $memoryStatus.TotalPhysical
        $availableBytes = [long] $memoryStatus.AvailablePhysical
    }

    if ($installedOk -or $statusOk) {
        $sourceStatus.nativeApi = 'success'
    }
    else {
        $sourceStatus.nativeApi = 'error'
        $warnings.Add('Windows memory APIs returned no data.')
    }
}
catch {
    $sourceStatus.nativeApi = 'error'
    $warnings.Add('Windows native memory inventory failed.')
}

$cpuCores = $null
$logicalProcessors = $null
$hypervisorPresent = $null
$virtualizationFirmwareEnabled = $null
$gpus = @()
$storage = @()
$osCaption = $null

try {
    $computerSystem = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction Stop
    $operatingSystem = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop
    $processor = Get-CimInstance -ClassName Win32_Processor -ErrorAction Stop | Select-Object -First 1
    $videoControllers = @(Get-CimInstance -ClassName Win32_VideoController -ErrorAction Stop)
    $diskDrives = @(Get-CimInstance -ClassName Win32_DiskDrive -ErrorAction Stop)

    if ($null -eq $manufacturer) { $manufacturer = $computerSystem.Manufacturer }
    if ($null -eq $model) { $model = $computerSystem.Model }
    if ($null -eq $cpuName) { $cpuName = $processor.Name }

    $osCaption = if ($null -eq $operatingSystem.Caption) { $null } else { [string] $operatingSystem.Caption }

    if ($null -eq $physicallyInstalledBytes) {
        $physicallyInstalledBytes = Convert-ToNullableInt64 $computerSystem.TotalPhysicalMemory
    }
    if ($null -eq $usableTotalBytes -and $null -ne $operatingSystem.TotalVisibleMemorySize) {
        $usableTotalBytes = [long] $operatingSystem.TotalVisibleMemorySize * 1KB
    }
    if ($null -eq $availableBytes -and $null -ne $operatingSystem.FreePhysicalMemory) {
        $availableBytes = [long] $operatingSystem.FreePhysicalMemory * 1KB
    }

    $cpuCores = Convert-ToNullableInt64 $processor.NumberOfCores
    $logicalProcessors = Convert-ToNullableInt64 $processor.NumberOfLogicalProcessors
    $hypervisorPresent = if ($null -eq $computerSystem.HypervisorPresent) { $null } else { [bool] $computerSystem.HypervisorPresent }
    $virtualizationFirmwareEnabled = if ($null -eq $processor.VirtualizationFirmwareEnabled) { $null } else { [bool] $processor.VirtualizationFirmwareEnabled }

    $gpus = @($videoControllers | ForEach-Object {
        [ordered]@{
            name               = if ($null -eq $_.Name) { $null } else { [string] $_.Name }
            driverVersion      = if ($null -eq $_.DriverVersion) { $null } else { [string] $_.DriverVersion }
            adapterMemoryBytes = Convert-ToNullableInt64 $_.AdapterRAM
        }
    })

    $storage = @($diskDrives | ForEach-Object {
        [ordered]@{
            model          = if ($null -eq $_.Model) { $null } else { ([string] $_.Model).Trim() }
            mediaType      = if ($null -eq $_.MediaType) { $null } else { [string] $_.MediaType }
            interfaceType  = if ($null -eq $_.InterfaceType) { $null } else { [string] $_.InterfaceType }
            sizeBytes      = Convert-ToNullableInt64 $_.Size
        }
    })

    $sourceStatus.cim = 'success'
}
catch {
    $sourceStatus.cim = 'unavailable'
    $warnings.Add('CIM/WMI inventory is unavailable in the current execution context.')
}

if ($null -eq $logicalProcessors -and $null -ne $env:NUMBER_OF_PROCESSORS) {
    $logicalProcessors = Convert-ToNullableInt64 $env:NUMBER_OF_PROCESSORS
}

$inventory = [ordered]@{
    schemaVersion    = '1.0.0'
    collectorVersion = '0.2.0-poc'
    collectedAtUtc   = [DateTime]::UtcNow.ToString('o')
    platform         = 'windows'
    privacy          = [ordered]@{
        identifiersCollected = $false
    }
    os               = [ordered]@{
        productName   = if ($null -eq $productName) { $null } else { [string] $productName }
        caption       = $osCaption
        editionId     = if ($null -eq $editionId) { $null } else { [string] $editionId }
        displayVersion = if ($null -eq $displayVersion) { $null } else { [string] $displayVersion }
        build         = if ($null -eq $build) { $null } else { [string] $build }
        ubr           = Convert-ToNullableInt64 $ubrRaw
        architecture  = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
        conflicts     = @($conflicts)
    }
    device           = [ordered]@{
        manufacturer  = if ($null -eq $manufacturer) { $null } else { [string] $manufacturer }
        model         = if ($null -eq $model) { $null } else { [string] $model }
        biosVersion   = if ($null -eq $biosVersion) { $null } else { [string] $biosVersion }
        biosReleaseDate = if ($null -eq $biosReleaseDate) { $null } else { [string] $biosReleaseDate }
    }
    cpu              = [ordered]@{
        name              = if ($null -eq $cpuName) { $null } else { ([string] $cpuName).Trim() }
        cores             = $cpuCores
        logicalProcessors = $logicalProcessors
    }
    memory           = [ordered]@{
        physicallyInstalledBytes = $physicallyInstalledBytes
        usableTotalBytes         = $usableTotalBytes
        availableBytes           = $availableBytes
    }
    gpus             = @($gpus)
    storage          = @($storage)
    capabilities     = [ordered]@{
        administrator                = $isAdministrator
        cimAvailable                 = ($sourceStatus.cim -eq 'success')
        hypervisorPresent            = $hypervisorPresent
        virtualizationFirmwareEnabled = $virtualizationFirmwareEnabled
    }
    sources          = $sourceStatus
    warnings         = @($warnings)
}

$json = $inventory | ConvertTo-Json -Depth 8

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $absoluteOutputPath = [IO.Path]::GetFullPath($OutputPath)
    $parent = Split-Path -Parent $absoluteOutputPath
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    [IO.File]::WriteAllText($absoluteOutputPath, $json, [Text.UTF8Encoding]::new($false))
}

$json
