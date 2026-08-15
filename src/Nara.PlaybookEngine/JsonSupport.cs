using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nara.PlaybookEngine;

internal static class JsonSupport
{
    internal static readonly JsonSerializerOptions StrictOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static readonly JsonSerializerOptions IndentedOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    internal static readonly JsonSerializerOptions CompactOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    internal static T DeserializeStrict<T>(byte[] utf8Json, string label)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(utf8Json, StrictOptions)
                ?? throw new InvalidDataException($"{label} is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{label} JSON is invalid or contains unsupported fields.", exception);
        }
    }

    internal static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    internal static byte[] SerializeIndented<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, IndentedOptions);

    internal static string SerializeCompact<T>(T value) =>
        JsonSerializer.Serialize(value, CompactOptions);

    internal static void WriteAtomically(string path, byte[] content)
    {
        string absolutePath = Path.GetFullPath(path);
        string? parent = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        string temporaryPath = absolutePath + ".nara-tmp";
        File.WriteAllBytes(temporaryPath, content);
        File.Move(temporaryPath, absolutePath, overwrite: true);
    }

    internal static void WriteReport(string path, TransactionReport report) =>
        WriteAtomically(path, SerializeIndented(report));

    internal static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    internal static string Utc(DateTimeOffset value) => value.UtcDateTime.ToString("O");
}
