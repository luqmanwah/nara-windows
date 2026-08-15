using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nara.ApprovalBroker;

internal static class BrokerJson
{
    internal static readonly JsonSerializerOptions Strict = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static readonly JsonSerializerOptions Indented = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    internal static T ReadStrict<T>(string path, string label)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllBytes(Path.GetFullPath(path)), Strict)
                ?? throw new InvalidDataException($"{label} is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{label} JSON is invalid or contains unsupported fields.", exception);
        }
    }

    internal static void Write<T>(string path, T value)
    {
        string absolutePath = Path.GetFullPath(path);
        string? parent = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        string temporaryPath = absolutePath + ".nara-tmp";
        File.WriteAllBytes(temporaryPath, JsonSerializer.SerializeToUtf8Bytes(value, Indented));
        File.Move(temporaryPath, absolutePath, overwrite: true);
    }

    internal static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    internal static string Utc(DateTimeOffset value) => value.UtcDateTime.ToString("O");
}
