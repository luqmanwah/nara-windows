namespace Nara.PolicyCompiler;

internal sealed record ActionPlan(
    string SchemaVersion,
    string CompilerVersion,
    string GeneratedAtUtc,
    string Status,
    InputHashes Input,
    DeviceSummary DeviceSummary,
    SelectedProfile SelectedProfile,
    IReadOnlyList<string> PolicyPrecedence,
    IReadOnlyList<PlannedAction> Actions,
    ActionSummary Summary);

internal sealed record InputHashes(string InventorySha256, string ProfileSha256);

internal sealed record DeviceSummary(
    string? OsBuild,
    long? InstalledRamBytes,
    int GpuCount,
    bool HardwareDataComplete);

internal sealed record SelectedProfile(
    string ProfileId,
    string ProfileVersion,
    string DeviceProfile,
    string ApprovalMode);

internal sealed record PlannedAction(
    string Id,
    string Category,
    string Title,
    string Description,
    string Decision,
    string Rationale,
    string Risk,
    bool RequiresAdmin,
    string Approval,
    string ResourceImpact,
    IReadOnlyList<string> Preconditions,
    IReadOnlyList<string> Verification,
    IReadOnlyList<string> Rollback,
    IReadOnlyList<string> Evidence);

internal sealed record ActionSummary(
    int Total,
    int Keep,
    int Recommend,
    int Defer,
    int Blocked,
    int ApprovalRequired);
