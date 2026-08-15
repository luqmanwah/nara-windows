using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nara.PolicyCompiler;

internal static class Program
{
    private static readonly JsonSerializerOptions OutputOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    internal static int Main(string[] args)
    {
        if (!TryParseArguments(args, out Arguments? parsed, out bool showHelp, out string? error))
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
            byte[] inventoryBytes = File.ReadAllBytes(Path.GetFullPath(parsed!.InventoryPath));
            byte[] profileBytes = File.ReadAllBytes(Path.GetFullPath(parsed.ProfilePath));
            InventoryEvidence inventory = InputLoader.LoadInventory(inventoryBytes);
            ProfileDocument profile = InputLoader.LoadProfile(profileBytes);

            ActionPlan plan = PolicyCompiler.Compile(
                profile,
                inventory,
                Sha256(inventoryBytes),
                Sha256(profileBytes),
                DateTimeOffset.UtcNow);

            string json = JsonSerializer.Serialize(plan, OutputOptions);
            string outputPath = Path.GetFullPath(parsed.OutputPath);
            string? parent = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllText(outputPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Console.Out.WriteLine(json);
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            Console.Error.WriteLine($"Compilation failed: {exception.Message}");
            return 1;
        }
    }

    private static string Sha256(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static bool TryParseArguments(
        IReadOnlyList<string> args,
        out Arguments? parsed,
        out bool showHelp,
        out string? error)
    {
        string? inventoryPath = null;
        string? profilePath = null;
        string? outputPath = null;
        parsed = null;
        showHelp = false;
        error = null;

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            if (argument is "--help" or "-h")
            {
                showHelp = true;
                continue;
            }

            if (argument is not ("--inventory" or "--profile" or "--output"))
            {
                error = $"Unknown argument: {argument}";
                return false;
            }

            if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                error = $"{argument} requires a file path.";
                return false;
            }

            string value = args[++index];
            switch (argument)
            {
                case "--inventory": inventoryPath = value; break;
                case "--profile": profilePath = value; break;
                case "--output": outputPath = value; break;
            }
        }

        if (showHelp)
        {
            return true;
        }

        if (inventoryPath is null || profilePath is null || outputPath is null)
        {
            error = "--inventory, --profile, and --output are required.";
            return false;
        }

        parsed = new Arguments(inventoryPath, profilePath, outputPath);
        return true;
    }

    private static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("Nara Policy Compiler 0.1.0");
        writer.WriteLine("Usage: Nara.PolicyCompiler --inventory <inventory.json> --profile <profile.json> --output <action-plan.json>");
        writer.WriteLine("Produces a read-only dry-run plan. It does not change Windows.");
    }

    private sealed record Arguments(string InventoryPath, string ProfilePath, string OutputPath);
}
