namespace Nara.PolicyCompiler;

internal sealed class ProfileDocument
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string ProfileId { get; init; } = string.Empty;
    public string ProfileVersion { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string DeviceProfile { get; init; } = string.Empty;
    public string ApprovalMode { get; init; } = string.Empty;
    public long MinimumRamBytes { get; init; }
    public string[] Goals { get; init; } = [];
    public ProfilePolicies Policies { get; init; } = new();
}

internal sealed class ProfilePolicies
{
    public UiPolicy Ui { get; init; } = new();
    public AiPolicy Ai { get; init; } = new();
    public SecurityPolicy Security { get; init; } = new();
    public UpdatePolicy Updates { get; init; } = new();
    public ServicePolicy Services { get; init; } = new();
    public DriverPolicy Drivers { get; init; } = new();
}

internal sealed class UiPolicy
{
    public string Animations { get; init; } = string.Empty;
    public string Transparency { get; init; } = string.Empty;
}

internal sealed class AiPolicy
{
    public string RuntimeMode { get; init; } = string.Empty;
    public int IdleUnloadMinutes { get; init; }
}

internal sealed class SecurityPolicy
{
    public string DefenderRealtimeProtection { get; init; } = string.Empty;
    public string MandatorySecurityUpdates { get; init; } = string.Empty;
}

internal sealed class UpdatePolicy
{
    public string CumulativeSecurity { get; init; } = string.Empty;
    public string OptionalPreview { get; init; } = string.Empty;
    public string FeatureUpdates { get; init; } = string.Empty;
}

internal sealed class ServicePolicy
{
    public string Strategy { get; init; } = string.Empty;
    public bool AllowBlanketDisable { get; init; }
}

internal sealed class DriverPolicy
{
    public string Source { get; init; } = string.Empty;
    public string OnMissingHardwareData { get; init; } = string.Empty;
}

internal sealed record InventoryEvidence(
    string? OsBuild,
    long? InstalledRamBytes,
    int GpuCount,
    bool CimAvailable,
    bool IdentifiersCollected,
    string SchemaVersion);
