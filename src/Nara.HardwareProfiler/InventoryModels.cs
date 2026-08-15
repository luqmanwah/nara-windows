namespace Nara.HardwareProfiler;

internal sealed record HardwareInventory(
    string SchemaVersion,
    string CollectorVersion,
    string CollectedAtUtc,
    string Platform,
    PrivacyInfo Privacy,
    OsInfo Os,
    DeviceInfo Device,
    CpuInfo Cpu,
    MemoryInfo Memory,
    IReadOnlyList<GpuInfo> Gpus,
    CapabilityInfo Capabilities,
    SourceInfo Sources,
    IReadOnlyList<string> Warnings);

internal sealed record PrivacyInfo(bool IdentifiersCollected);

internal sealed record OsInfo(
    string? ProductName,
    string? EditionId,
    string? DisplayVersion,
    string? Build,
    long? Ubr,
    string Architecture,
    IReadOnlyList<string> Conflicts);

internal sealed record DeviceInfo(
    string? Manufacturer,
    string? Model,
    string? BiosVersion,
    string? BiosReleaseDate);

internal sealed record CpuInfo(
    string? Name,
    long? Cores,
    long? LogicalProcessors);

internal sealed record MemoryInfo(
    long? PhysicallyInstalledBytes,
    long? UsableTotalBytes,
    long? AvailableBytes);

internal sealed record GpuInfo(
    string? Name,
    string? DriverVersion,
    long? AdapterMemoryBytes);

internal sealed record CapabilityInfo(
    bool Administrator,
    bool CimAvailable,
    bool? HypervisorPresent,
    bool? VirtualizationFirmwareEnabled);

internal sealed record SourceInfo(
    string Registry,
    string NativeApi,
    string Cim);

internal sealed record RegistrySnapshot(
    string? ProductName,
    string? EditionId,
    string? DisplayVersion,
    string? Build,
    long? Ubr,
    string? CpuName,
    string? Manufacturer,
    string? Model,
    string? BiosVersion,
    string? BiosReleaseDate,
    string Status);

internal sealed record NativeMemorySnapshot(
    long? PhysicallyInstalledBytes,
    long? UsableTotalBytes,
    long? AvailableBytes,
    string Status);

