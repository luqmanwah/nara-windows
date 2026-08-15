using System.Text;
using System.Text.Json;

namespace Nara.HardwareProfiler;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    internal static int Main(string[] args)
    {
        if (!TryParseArguments(args, out string? outputPath, out bool showHelp, out string? error))
        {
            Console.Error.WriteLine(error);
            PrintHelp(Console.Error);
            return 2;
        }

        if (showHelp)
        {
            PrintHelp(Console.Out);
            return 0;
        }

        try
        {
            HardwareInventory inventory = HardwareInventoryCollector.Collect();
            string json = JsonSerializer.Serialize(inventory, JsonOptions);

            if (outputPath is not null)
            {
                string absolutePath = Path.GetFullPath(outputPath);
                string? parent = Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                File.WriteAllText(absolutePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }

            Console.Out.WriteLine(json);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Inventory failed: {exception.GetType().Name}");
            return 1;
        }
    }

    private static bool TryParseArguments(
        IReadOnlyList<string> args,
        out string? outputPath,
        out bool showHelp,
        out string? error)
    {
        outputPath = null;
        showHelp = false;
        error = null;

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "--output":
                    if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
                    {
                        error = "--output requires a file path.";
                        return false;
                    }

                    outputPath = args[++index];
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    error = $"Unknown argument: {argument}";
                    return false;
            }
        }

        return true;
    }

    private static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("Nara Hardware Profiler 0.1.0");
        writer.WriteLine("Usage: Nara.HardwareProfiler [--output <inventory.json>]");
        writer.WriteLine("The collector is read-only and excludes personal identifiers.");
    }
}

