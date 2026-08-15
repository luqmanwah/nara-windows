using System.Text.Json;

namespace Nara.PlaybookEngine;

internal sealed class PlaybookDocument
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string PlaybookId { get; init; } = string.Empty;
    public string PlaybookVersion { get; init; } = string.Empty;
    public string Adapter { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public PlaybookAction[] Actions { get; init; } = [];
}

internal sealed class PlaybookAction
{
    public string RuleId { get; init; } = string.Empty;
    public SettingOperation[] Operations { get; init; } = [];
}

internal sealed class SettingOperation
{
    public string Setting { get; init; } = string.Empty;
    public string ValueType { get; init; } = string.Empty;
    public JsonElement Expected { get; init; }
    public JsonElement Desired { get; init; }
}

internal sealed class PlaybookSignature
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string KeyId { get; init; } = string.Empty;
    public string Algorithm { get; init; } = string.Empty;
    public string ContentSha256 { get; init; } = string.Empty;
    public string SignatureBase64 { get; init; } = string.Empty;
}

internal sealed class TrustManifest
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string ManifestId { get; init; } = string.Empty;
    public string ManifestVersion { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;
    public string IssuedAtUtc { get; init; } = string.Empty;
    public string ExpiresAtUtc { get; init; } = string.Empty;
    public TrustKey[] Keys { get; init; } = [];
    public string[] RevokedKeyIds { get; init; } = [];
}

internal sealed class TrustKey
{
    public string KeyId { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public string Algorithm { get; init; } = string.Empty;
    public string PublicKeySpkiBase64 { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string NotBeforeUtc { get; init; } = string.Empty;
    public string NotAfterUtc { get; init; } = string.Empty;
}

internal sealed class TrustManifestSignature
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string RootKeyId { get; init; } = string.Empty;
    public string Algorithm { get; init; } = string.Empty;
    public string ContentSha256 { get; init; } = string.Empty;
    public string SignatureBase64 { get; init; } = string.Empty;
}

internal sealed class FakeWindowsState
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string Adapter { get; init; } = string.Empty;
    public string InstallationId { get; init; } = string.Empty;
    public string? ApprovalKeyId { get; init; }
    public string? ApprovalPublicKeySpkiBase64 { get; init; }
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

internal sealed record PlanActionEvidence(
    string Id,
    string Decision,
    string Approval,
    string Risk,
    bool RequiresAdmin);

internal sealed record ActionPlanEvidence(
    string SchemaVersion,
    string Status,
    IReadOnlyDictionary<string, PlanActionEvidence> Actions);

internal sealed record TransactionReport(
    string SchemaVersion,
    string EngineVersion,
    string TransactionId,
    bool Simulation,
    string Status,
    string StartedAtUtc,
    string FinishedAtUtc,
    string ActionPlanSha256,
    string PlaybookSha256,
    string PlaybookSignatureKeyId,
    string ApprovalId,
    string ApprovalSessionNonce,
    string InstallationId,
    string ConsentRequestSha256,
    bool Changed,
    string StateBeforeSha256,
    string StateAfterSha256,
    IReadOnlyList<string> AppliedActionIds,
    IReadOnlyList<string> RevertedActionIds,
    IReadOnlyList<string> Errors,
    long LedgerFirstSequence,
    long LedgerLastSequence);

internal sealed record ExecutionRequest(
    string ActionPlanPath,
    string PlaybookPath,
    string SignaturePath,
    string TrustManifestPath,
    string TrustManifestSignaturePath,
    string ApprovalPath,
    string StatePath,
    string LedgerPath,
    string ReportPath,
    string? TestFailAfterAction);

internal sealed record ExecutionResult(string Status, int ExitCode, TransactionReport Report);
