using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nara.PolicyCompiler;

internal static class InputLoader
{
    private static readonly JsonSerializerOptions StrictProfileOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static ProfileDocument LoadProfile(byte[] utf8Json)
    {
        ProfileDocument profile;
        try
        {
            profile = JsonSerializer.Deserialize<ProfileDocument>(utf8Json, StrictProfileOptions)
                ?? throw new InvalidDataException("Profile is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Profile JSON is invalid or contains unsupported fields.", exception);
        }

        ValidateProfile(profile);
        return profile;
    }

    internal static InventoryEvidence LoadInventory(byte[] utf8Json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8Json);
            JsonElement root = document.RootElement;
            string schemaVersion = RequiredString(root, "schemaVersion");
            bool identifiersCollected = root.GetProperty("privacy").GetProperty("identifiersCollected").GetBoolean();
            string? osBuild = OptionalString(root.GetProperty("os"), "build");
            long? installedRamBytes = OptionalInt64(root.GetProperty("memory"), "physicallyInstalledBytes");
            int gpuCount = root.GetProperty("gpus").GetArrayLength();
            bool cimAvailable = root.GetProperty("capabilities").GetProperty("cimAvailable").GetBoolean();

            if (schemaVersion != "1.0.0")
            {
                throw new InvalidDataException($"Unsupported inventory schema version: {schemaVersion}");
            }

            if (identifiersCollected)
            {
                throw new InvalidDataException("Inventory violates the privacy contract.");
            }

            return new InventoryEvidence(
                osBuild,
                installedRamBytes,
                gpuCount,
                cimAvailable,
                identifiersCollected,
                schemaVersion);
        }
        catch (Exception exception) when (
            exception is JsonException
            or KeyNotFoundException
            or InvalidOperationException
            or FormatException
            or OverflowException)
        {
            throw new InvalidDataException("Hardware inventory JSON is invalid or incomplete.", exception);
        }
    }

    private static void ValidateProfile(ProfileDocument profile)
    {
        Require(profile.Policies is not null, "Profile policies are required.");
        Require(profile.Policies.Ui is not null, "UI policy is required.");
        Require(profile.Policies.Ai is not null, "AI policy is required.");
        Require(profile.Policies.Security is not null, "Security policy is required.");
        Require(profile.Policies.Updates is not null, "Update policy is required.");
        Require(profile.Policies.Services is not null, "Service policy is required.");
        Require(profile.Policies.Drivers is not null, "Driver policy is required.");
        Require(profile.SchemaVersion == "1.0.0", "Unsupported profile schema version.");
        Require(profile.ProfileId == "lite-recommended", "Profile ID is not supported by compiler 0.1.0.");
        Require(profile.ProfileVersion == "1.0.0", "Profile version is not supported by compiler 0.1.0.");
        Require(profile.DeviceProfile == "lite", "Compiler 0.1.0 accepts only the Lite device profile.");
        Require(profile.ApprovalMode == "recommended", "Compiler 0.1.0 accepts only Recommended approval mode.");
        Require(profile.MinimumRamBytes >= 4L * 1024 * 1024 * 1024, "minimumRamBytes is below the safety floor.");
        Require(profile.Goals is not null && profile.Goals.Length > 0 && profile.Goals.All(goal => !string.IsNullOrWhiteSpace(goal)), "At least one profile goal is required.");
        Require(profile.Policies.Ui.Animations == "off", "Lite animations policy must be off.");
        Require(profile.Policies.Ui.Transparency == "off", "Lite transparency policy must be off.");
        Require(profile.Policies.Ai.RuntimeMode == "on-demand", "Lite AI runtime must be on-demand.");
        Require(profile.Policies.Ai.IdleUnloadMinutes is >= 1 and <= 120, "AI idle unload window is invalid.");
        Require(profile.Policies.Security.DefenderRealtimeProtection == "keep", "Defender real-time protection cannot be weakened.");
        Require(profile.Policies.Security.MandatorySecurityUpdates == "keep", "Mandatory security updates cannot be weakened.");
        Require(profile.Policies.Updates.CumulativeSecurity is "install" or "recommend", "Cumulative security update policy is invalid.");
        Require(profile.Policies.Updates.OptionalPreview is "defer" or "manual", "Optional preview policy is invalid.");
        Require(profile.Policies.Updates.FeatureUpdates is "defer" or "manual" or "recommend", "Feature update policy is invalid.");
        Require(profile.Policies.Services.Strategy == "safe-trigger-start", "Lite service strategy must use the safe allowlist.");
        Require(!profile.Policies.Services.AllowBlanketDisable, "Blanket service disabling is forbidden.");
        Require(profile.Policies.Drivers.Source == "official-vendor", "Only official vendor driver sources are supported.");
        Require(profile.Policies.Drivers.OnMissingHardwareData == "block-and-report", "Missing hardware data must block driver selection.");
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        string? value = OptionalString(element, propertyName);
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException($"Required field is empty: {propertyName}");
    }

    private static string? OptionalString(JsonElement element, string propertyName)
    {
        JsonElement property = element.GetProperty(propertyName);
        return property.ValueKind == JsonValueKind.Null ? null : property.GetString();
    }

    private static long? OptionalInt64(JsonElement element, string propertyName)
    {
        JsonElement property = element.GetProperty(propertyName);
        return property.ValueKind == JsonValueKind.Null ? null : property.GetInt64();
    }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
