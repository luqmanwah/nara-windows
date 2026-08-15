namespace Nara.ApprovalBroker;

internal sealed class FakeApprovalKey
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string Protection { get; init; } = string.Empty;
    public string KeyId { get; init; } = string.Empty;
    public string InstallationId { get; init; } = string.Empty;
    public string Algorithm { get; init; } = string.Empty;
    public string CreatedAtUtc { get; init; } = string.Empty;
    public string PublicKeySpkiBase64 { get; init; } = string.Empty;
    public string PrivateKeyPkcs8Base64 { get; init; } = string.Empty;
}

internal sealed class FakeWindowsState
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string Adapter { get; init; } = string.Empty;
    public string InstallationId { get; set; } = string.Empty;
    public string? ApprovalKeyId { get; set; }
    public string? ApprovalPublicKeySpkiBase64 { get; set; }
    public int Revision { get; set; }
    public FakeWindowsSettings Settings { get; init; } = new();
}

internal sealed class FakeWindowsSettings
{
    public bool UiAnimations { get; set; }
    public bool UiTransparency { get; set; }
    public string AiRuntimeMode { get; set; } = string.Empty;
    public int AiIdleUnloadMinutes { get; set; }
}

internal sealed record PreparedInputs(
    string InstallationId,
    string BrokerKeyId,
    string ActionPlanSha256,
    string PlaybookSha256,
    string PlaybookSignatureKeyId,
    string ApprovalMode,
    Nara.ApprovalContracts.ConsentAction[] Actions);
