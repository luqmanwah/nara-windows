using System.Globalization;
using Microsoft.Win32;

namespace Nara.HardwareProfiler;

internal static class RegistryCollector
{
    private const string CurrentVersionPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const string BiosPath = @"HARDWARE\DESCRIPTION\System\BIOS";
    private const string CpuPath = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";

    internal static RegistrySnapshot Collect(ICollection<string> warnings)
    {
        try
        {
            using RegistryKey? currentVersion = Registry.LocalMachine.OpenSubKey(CurrentVersionPath, writable: false);
            using RegistryKey? bios = Registry.LocalMachine.OpenSubKey(BiosPath, writable: false);
            using RegistryKey? cpu = Registry.LocalMachine.OpenSubKey(CpuPath, writable: false);

            string? productName = ReadString(currentVersion, "ProductName");
            string? build = ReadString(currentVersion, "CurrentBuild");

            string status = currentVersion is not null && (productName is not null || build is not null)
                ? "success"
                : "partial";

            if (status == "partial")
            {
                warnings.Add("Windows registry inventory returned partial data.");
            }

            return new RegistrySnapshot(
                ProductName: productName,
                EditionId: ReadString(currentVersion, "EditionID"),
                DisplayVersion: ReadString(currentVersion, "DisplayVersion"),
                Build: build,
                Ubr: ReadInt64(currentVersion, "UBR"),
                CpuName: ReadString(cpu, "ProcessorNameString")?.Trim(),
                Manufacturer: ReadString(bios, "SystemManufacturer"),
                Model: ReadString(bios, "SystemProductName"),
                BiosVersion: ReadString(bios, "BIOSVersion"),
                BiosReleaseDate: ReadString(bios, "BIOSReleaseDate"),
                Status: status);
        }
        catch
        {
            warnings.Add("Windows registry inventory is unavailable.");
            return new RegistrySnapshot(null, null, null, null, null, null, null, null, null, null, "unavailable");
        }
    }

    private static string? ReadString(RegistryKey? key, string name)
    {
        object? value = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value switch
        {
            null => null,
            string text => text,
            string[] values => string.Join(" | ", values),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    private static long? ReadInt64(RegistryKey? key, string name)
    {
        object? value = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }
}

