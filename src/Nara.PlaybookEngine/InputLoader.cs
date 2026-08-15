using System.Text.Json;
using Nara.ApprovalContracts;

namespace Nara.PlaybookEngine;

internal static class InputLoader
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedSettingsByRule =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["NARA-LITE-UI-001"] = ["ui.animations"],
            ["NARA-LITE-UI-002"] = ["ui.transparency"],
            ["NARA-LITE-AI-001"] = ["ai.runtimeMode", "ai.idleUnloadMinutes"]
        };

    internal static ActionPlanEvidence LoadActionPlan(byte[] utf8Json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8Json);
            JsonElement root = document.RootElement;
            string schemaVersion = RequiredString(root, "schemaVersion");
            string status = RequiredString(root, "status");
            Dictionary<string, PlanActionEvidence> actions = new(StringComparer.Ordinal);

            foreach (JsonElement item in root.GetProperty("actions").EnumerateArray())
            {
                string id = RequiredString(item, "id");
                PlanActionEvidence action = new(
                    id,
                    RequiredString(item, "decision"),
                    RequiredString(item, "approval"),
                    RequiredString(item, "risk"),
                    item.GetProperty("requiresAdmin").GetBoolean());
                JsonSupport.Require(actions.TryAdd(id, action), $"Action plan contains duplicate rule ID: {id}");
            }

            JsonSupport.Require(schemaVersion == "1.0.0", "Unsupported action-plan schema version.");
            JsonSupport.Require(status == "dry-run", "Only a dry-run action plan can enter approval.");
            JsonSupport.Require(actions.Count > 0, "Action plan has no actions.");
            return new ActionPlanEvidence(schemaVersion, status, actions);
        }
        catch (Exception exception) when (
            exception is JsonException
            or KeyNotFoundException
            or InvalidOperationException
            or FormatException
            or OverflowException)
        {
            throw new InvalidDataException("Action plan JSON is invalid or incomplete.", exception);
        }
    }

    internal static PlaybookDocument LoadPlaybook(byte[] utf8Json)
    {
        PlaybookDocument playbook = JsonSupport.DeserializeStrict<PlaybookDocument>(utf8Json, "Playbook");
        JsonSupport.Require(playbook.Actions is not null, "Playbook actions are required.");
        JsonSupport.Require(playbook.SchemaVersion == "1.0.0", "Unsupported playbook schema version.");
        JsonSupport.Require(playbook.PlaybookId == "lite-safe-settings", "Unsupported playbook ID.");
        JsonSupport.Require(playbook.PlaybookVersion == "0.1.0", "Unsupported playbook version.");
        JsonSupport.Require(playbook.Adapter == "fake-windows", "Engine 0.3.0 accepts only the fake-windows adapter.");
        JsonSupport.Require(playbook.Actions.Length > 0, "Playbook has no actions.");

        HashSet<string> actionIds = new(StringComparer.Ordinal);
        HashSet<string> settingIds = new(StringComparer.Ordinal);
        foreach (PlaybookAction action in playbook.Actions)
        {
            JsonSupport.Require(action is not null, "Playbook action cannot be null.");
            JsonSupport.Require(actionIds.Add(action.RuleId), $"Duplicate playbook rule ID: {action.RuleId}");
            JsonSupport.Require(AllowedSettingsByRule.TryGetValue(action.RuleId, out string[]? allowed), $"Rule is not allowlisted: {action.RuleId}");
            JsonSupport.Require(action.Operations is not null && action.Operations.Length > 0, $"Rule has no operations: {action.RuleId}");

            HashSet<string> actionSettings = action.Operations.Select(operation => operation.Setting).ToHashSet(StringComparer.Ordinal);
            JsonSupport.Require(
                actionSettings.SetEquals(allowed),
                $"Rule settings do not match the signed adapter mapping: {action.RuleId}");

            foreach (SettingOperation operation in action.Operations)
            {
                JsonSupport.Require(operation is not null, $"Rule contains a null operation: {action.RuleId}");
                JsonSupport.Require(settingIds.Add(operation.Setting), $"Setting is targeted more than once: {operation.Setting}");
                ValidateOperation(operation);
            }
        }

        return playbook;
    }

    internal static ApprovalReceiptV3 LoadApproval(byte[] utf8Json, FakeWindowsState state, DateTimeOffset nowUtc)
    {
        ApprovalReceiptV3 approval = JsonSupport.DeserializeStrict<ApprovalReceiptV3>(utf8Json, "Approval receipt");
        JsonSupport.Require(approval.ApprovedActionIds is not null, "Approved action IDs are required.");
        JsonSupport.Require(approval.SchemaVersion == "3.0.0", "Unsupported approval schema version.");
        JsonSupport.Require(Guid.TryParse(approval.ApprovalId, out _), "Approval ID is invalid.");
        JsonSupport.Require(Guid.TryParse(approval.SessionNonce, out _), "Approval session nonce is invalid.");
        JsonSupport.Require(Guid.TryParse(approval.InstallationId, out _), "Approval installation ID is invalid.");
        JsonSupport.Require(approval.Scope == "simulation-only", "Engine 0.3.0 accepts only simulation approval.");
        JsonSupport.Require(approval.ActorType == "local-user-simulation", "Engine 0.3.0 accepts only local-user simulation approval.");
        JsonSupport.Require(approval.ApprovalMode == "recommended", "Engine 0.3.0 accepts only Recommended approval receipts.");
        JsonSupport.Require(approval.SingleUse, "Approval receipt must be single-use.");
        JsonSupport.Require(approval.ConsentTextVersion == "broker-v1", "Unsupported consent text version.");
        JsonSupport.Require(IsSha256(approval.ConsentRequestSha256), "Approval consent-request hash is invalid.");
        JsonSupport.Require(IsSha256(approval.ActionPlanSha256), "Approval action-plan hash is invalid.");
        JsonSupport.Require(IsSha256(approval.PlaybookSha256), "Approval playbook hash is invalid.");
        JsonSupport.Require(Guid.TryParse(approval.LocalApprovalKeyId, out _), "Local approval key ID is invalid.");
        JsonSupport.Require(approval.SignatureAlgorithm == "ecdsa-p256-sha256", "Local approval signature algorithm is invalid.");
        JsonSupport.Require(state.ApprovalKeyId is not null && state.ApprovalPublicKeySpkiBase64 is not null, "Installation has no local approval key.");
        JsonSupport.Require(approval.LocalApprovalKeyId == state.ApprovalKeyId, "Approval was signed by another local key.");
        JsonSupport.Require(ApprovalCryptography.VerifyReceipt(approval, state.ApprovalPublicKeySpkiBase64), "Local approval receipt signature is invalid.");
        JsonSupport.Require(approval.ApprovedActionIds.Length > 0, "Approval contains no action IDs.");
        JsonSupport.Require(
            approval.ApprovedActionIds.Distinct(StringComparer.Ordinal).Count() == approval.ApprovedActionIds.Length,
            "Approval contains duplicate action IDs.");
        JsonSupport.Require(DateTimeOffset.TryParse(approval.IssuedAtUtc, out DateTimeOffset issuedAt), "Approval issue time is invalid.");
        JsonSupport.Require(DateTimeOffset.TryParse(approval.ExpiresAtUtc, out DateTimeOffset expiresAt), "Approval expiry time is invalid.");
        JsonSupport.Require(expiresAt > issuedAt, "Approval expiry must be later than issue time.");
        JsonSupport.Require(expiresAt <= issuedAt.AddMinutes(10), "Approval lifetime cannot exceed 10 minutes.");
        JsonSupport.Require(nowUtc >= issuedAt.AddMinutes(-1), "Approval was issued too far in the future.");
        JsonSupport.Require(nowUtc <= expiresAt, "Approval has expired.");
        return approval;
    }

    internal static PlaybookSignature LoadAndVerifySignature(
        byte[] utf8Json,
        byte[] playbookBytes,
        TrustManifest manifest,
        DateTimeOffset nowUtc)
    {
        PlaybookSignature signature = JsonSupport.DeserializeStrict<PlaybookSignature>(utf8Json, "Playbook signature");
        TrustedPlaybookKeys.VerifyPlaybook(playbookBytes, signature, manifest, nowUtc);
        return signature;
    }

    internal static TrustManifest LoadAndVerifyTrustManifest(
        byte[] manifestBytes,
        byte[] manifestSignatureBytes,
        DateTimeOffset nowUtc)
    {
        TrustManifest manifest = JsonSupport.DeserializeStrict<TrustManifest>(manifestBytes, "Trust manifest");
        TrustManifestSignature signature = JsonSupport.DeserializeStrict<TrustManifestSignature>(manifestSignatureBytes, "Trust manifest signature");
        TrustedPlaybookKeys.VerifyManifest(manifestBytes, signature);
        TrustedPlaybookKeys.ValidateManifest(manifest, nowUtc);
        return manifest;
    }

    internal static FakeWindowsState LoadState(byte[] utf8Json)
    {
        FakeWindowsState state = JsonSupport.DeserializeStrict<FakeWindowsState>(utf8Json, "Fake Windows state");
        JsonSupport.Require(state.Settings is not null, "Fake Windows settings are required.");
        JsonSupport.Require(state.SchemaVersion == "1.0.0", "Unsupported fake state schema version.");
        JsonSupport.Require(state.Adapter == "fake-windows", "State adapter must be fake-windows.");
        JsonSupport.Require(Guid.TryParse(state.InstallationId, out _), "Fake state installation ID is invalid.");
        JsonSupport.Require(Guid.TryParse(state.ApprovalKeyId, out _), "Fake state approval key ID is invalid.");
        JsonSupport.Require(!string.IsNullOrWhiteSpace(state.ApprovalPublicKeySpkiBase64), "Fake state approval public key is missing.");
        JsonSupport.Require(state.Revision >= 0, "State revision cannot be negative.");
        JsonSupport.Require(state.Settings.AiRuntimeMode is "disabled" or "on-demand" or "always-on", "Fake AI runtime mode is invalid.");
        JsonSupport.Require(state.Settings.AiIdleUnloadMinutes is >= 1 and <= 120, "Fake AI idle unload window is invalid.");
        return state;
    }

    private static void ValidateOperation(SettingOperation operation)
    {
        JsonValueKind expectedKind;
        switch (operation.ValueType)
        {
            case "boolean":
                expectedKind = JsonValueKind.True;
                JsonSupport.Require(IsBoolean(operation.Expected) && IsBoolean(operation.Desired), $"Boolean value mismatch: {operation.Setting}");
                break;
            case "string":
                expectedKind = JsonValueKind.String;
                JsonSupport.Require(operation.Expected.ValueKind == expectedKind && operation.Desired.ValueKind == expectedKind, $"String value mismatch: {operation.Setting}");
                break;
            case "integer":
                expectedKind = JsonValueKind.Number;
                JsonSupport.Require(operation.Expected.ValueKind == expectedKind && operation.Desired.ValueKind == expectedKind, $"Integer value mismatch: {operation.Setting}");
                JsonSupport.Require(operation.Expected.TryGetInt32(out _) && operation.Desired.TryGetInt32(out _), $"Integer is outside the supported range: {operation.Setting}");
                break;
            default:
                throw new InvalidDataException($"Unsupported value type: {operation.ValueType}");
        }

        if (operation.Setting == "ai.runtimeMode")
        {
            JsonSupport.Require(IsAiMode(operation.Expected.GetString()) && IsAiMode(operation.Desired.GetString()), "AI runtime mode is invalid.");
        }
        else if (operation.Setting == "ai.idleUnloadMinutes")
        {
            JsonSupport.Require(operation.Desired.GetInt32() is >= 1 and <= 120, "AI idle unload window is invalid.");
        }
    }

    private static bool IsBoolean(JsonElement element) =>
        element.ValueKind is JsonValueKind.True or JsonValueKind.False;

    private static bool IsAiMode(string? value) => value is "disabled" or "on-demand" or "always-on";

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string RequiredString(JsonElement element, string propertyName)
    {
        string? value = element.GetProperty(propertyName).GetString();
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException($"Required field is empty: {propertyName}");
    }
}
