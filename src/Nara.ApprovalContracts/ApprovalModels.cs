namespace Nara.ApprovalContracts;

public sealed class ApprovalReceiptV3
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string ApprovalId { get; init; } = string.Empty;
    public string SessionNonce { get; init; } = string.Empty;
    public string InstallationId { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string ActorType { get; init; } = string.Empty;
    public string ApprovalMode { get; init; } = string.Empty;
    public string ConsentRequestSha256 { get; init; } = string.Empty;
    public string ActionPlanSha256 { get; init; } = string.Empty;
    public string PlaybookSha256 { get; init; } = string.Empty;
    public string PlaybookSignatureKeyId { get; init; } = string.Empty;
    public string[] ApprovedActionIds { get; init; } = [];
    public string IssuedAtUtc { get; init; } = string.Empty;
    public string ExpiresAtUtc { get; init; } = string.Empty;
    public bool SingleUse { get; init; }
    public string ConsentTextVersion { get; init; } = string.Empty;
    public string LocalApprovalKeyId { get; init; } = string.Empty;
    public string SignatureAlgorithm { get; init; } = string.Empty;
    public string SignatureBase64 { get; set; } = string.Empty;
}

public sealed class ConsentRequest
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string RequestId { get; init; } = string.Empty;
    public string InstallationId { get; init; } = string.Empty;
    public string BrokerKeyId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string CreatedAtUtc { get; init; } = string.Empty;
    public string ExpiresAtUtc { get; init; } = string.Empty;
    public string ActionPlanSha256 { get; init; } = string.Empty;
    public string PlaybookSha256 { get; init; } = string.Empty;
    public string PlaybookSignatureKeyId { get; init; } = string.Empty;
    public string ApprovalMode { get; init; } = string.Empty;
    public ConsentAction[] Actions { get; init; } = [];
    public string ConsentStatement { get; init; } = string.Empty;
    public string SignatureAlgorithm { get; init; } = string.Empty;
    public string RequestSignatureBase64 { get; set; } = string.Empty;
}

public sealed class ConsentAction
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Risk { get; init; } = string.Empty;
    public bool RequiresAdmin { get; init; }
    public string ResourceImpact { get; init; } = string.Empty;
    public string[] Verification { get; init; } = [];
    public string[] Rollback { get; init; } = [];
}
