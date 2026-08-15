using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Nara.HardwareProfiler;

internal static class HardwareInventoryCollector
{
    private const string CollectorVersion = "0.1.0";

    internal static HardwareInventory Collect()
    {
        List<string> warnings = [];
        List<string> conflicts = [];

        RegistrySnapshot registry = RegistryCollector.Collect(warnings);
        NativeMemorySnapshot memory = NativeMemoryCollector.Collect(warnings);

        if (int.TryParse(registry.Build, NumberStyles.None, CultureInfo.InvariantCulture, out int buildNumber)
            && buildNumber >= 22000
            && registry.ProductName?.Contains("Windows 10", StringComparison.OrdinalIgnoreCase) == true)
        {
            conflicts.Add("Registry product name conflicts with the detected build family.");
        }

        bool administrator = IsAdministrator(warnings);

        warnings.Add("CIM adapter is unavailable in collector 0.1.0; GPU, physical core, and virtualization fields may be incomplete.");

        return new HardwareInventory(
            SchemaVersion: "1.0.0",
            CollectorVersion: CollectorVersion,
            CollectedAtUtc: DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Platform: "windows",
            Privacy: new PrivacyInfo(IdentifiersCollected: false),
            Os: new OsInfo(
                registry.ProductName,
                registry.EditionId,
                registry.DisplayVersion,
                registry.Build,
                registry.Ubr,
                RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
                conflicts),
            Device: new DeviceInfo(
                registry.Manufacturer,
                registry.Model,
                registry.BiosVersion,
                registry.BiosReleaseDate),
            Cpu: new CpuInfo(
                registry.CpuName,
                Cores: null,
                LogicalProcessors: Environment.ProcessorCount),
            Memory: new MemoryInfo(
                memory.PhysicallyInstalledBytes,
                memory.UsableTotalBytes,
                memory.AvailableBytes),
            Gpus: Array.Empty<GpuInfo>(),
            Capabilities: new CapabilityInfo(
                Administrator: administrator,
                CimAvailable: false,
                HypervisorPresent: null,
                VirtualizationFirmwareEnabled: null),
            Sources: new SourceInfo(
                Registry: registry.Status,
                NativeApi: memory.Status,
                Cim: "unavailable"),
            Warnings: warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static bool IsAdministrator(ICollection<string> warnings)
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            warnings.Add("Administrator status could not be determined.");
            return false;
        }
    }
}

