using System.Runtime.InteropServices;

namespace Nara.HardwareProfiler;

internal static class NativeMemoryCollector
{
    internal static NativeMemorySnapshot Collect(ICollection<string> warnings)
    {
        try
        {
            bool installedOk = NativeMethods.GetPhysicallyInstalledSystemMemory(out ulong installedKilobytes);

            MemoryStatusEx memoryStatus = new()
            {
                Length = checked((uint)Marshal.SizeOf<MemoryStatusEx>())
            };
            bool statusOk = NativeMethods.GlobalMemoryStatusEx(ref memoryStatus);

            long? installedBytes = installedOk ? ToInt64Bytes(installedKilobytes, 1024UL) : null;
            long? usableBytes = statusOk ? ToInt64(memoryStatus.TotalPhysical) : null;
            long? availableBytes = statusOk ? ToInt64(memoryStatus.AvailablePhysical) : null;

            if (!installedOk && !statusOk)
            {
                warnings.Add("Windows native memory APIs returned no data.");
                return new NativeMemorySnapshot(null, null, null, "error");
            }

            return new NativeMemorySnapshot(installedBytes, usableBytes, availableBytes, "success");
        }
        catch
        {
            warnings.Add("Windows native memory inventory failed.");
            return new NativeMemorySnapshot(null, null, null, "error");
        }
    }

    private static long? ToInt64(ulong value) => value <= long.MaxValue ? (long)value : null;

    private static long? ToInt64Bytes(ulong value, ulong multiplier)
    {
        if (value > (ulong)long.MaxValue / multiplier)
        {
            return null;
        }

        return (long)(value * multiplier);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        internal uint Length;
        internal uint MemoryLoad;
        internal ulong TotalPhysical;
        internal ulong AvailablePhysical;
        internal ulong TotalPageFile;
        internal ulong AvailablePageFile;
        internal ulong TotalVirtual;
        internal ulong AvailableVirtual;
        internal ulong AvailableExtendedVirtual;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);
    }
}

