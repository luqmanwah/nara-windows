using System.Text.Json;
using Nara.ApprovalContracts;

namespace Nara.ApprovalBroker;

internal static class ConsentCompiler
{
    internal static PreparedInputs Prepare(
        byte[] actionPlanBytes,
        byte[] playbookBytes,
        byte[] playbookSignatureBytes,
        FakeWindowsState state,
        FakeApprovalKey key)
    {
        BrokerJson.Require(state.ApprovalKeyId is not null && state.ApprovalPublicKeySpkiBase64 is not null, "Fake state has no initialized approval key.");
        BrokerJson.Require(state.InstallationId == key.InstallationId, "State and simulation key installation IDs do not match.");
        BrokerJson.Require(state.ApprovalKeyId == key.KeyId, "State and simulation key IDs do not match.");
        BrokerJson.Require(state.ApprovalPublicKeySpkiBase64 == key.PublicKeySpkiBase64, "State and simulation public keys do not match.");

        using JsonDocument planDocument = JsonDocument.Parse(actionPlanBytes);
        using JsonDocument playbookDocument = JsonDocument.Parse(playbookBytes);
        using JsonDocument signatureDocument = JsonDocument.Parse(playbookSignatureBytes);
        JsonElement planRoot = planDocument.RootElement;
        JsonElement playbookRoot = playbookDocument.RootElement;

        BrokerJson.Require(planRoot.GetProperty("status").GetString() == "dry-run", "Consent requires a dry-run action plan.");
        string approvalMode = planRoot.GetProperty("selectedProfile").GetProperty("approvalMode").GetString() ?? string.Empty;
        BrokerJson.Require(approvalMode == "recommended", "Approval Broker 0.1.0 accepts only Recommended mode.");

        HashSet<string> playbookActionIds = playbookRoot.GetProperty("actions")
            .EnumerateArray()
            .Select(item => item.GetProperty("ruleId").GetString() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
        BrokerJson.Require(playbookActionIds.Count > 0 && !playbookActionIds.Contains(string.Empty), "Playbook action IDs are invalid.");

        List<ConsentAction> consentActions = [];
        foreach (JsonElement action in planRoot.GetProperty("actions").EnumerateArray())
        {
            string id = action.GetProperty("id").GetString() ?? string.Empty;
            if (!playbookActionIds.Contains(id))
            {
                continue;
            }

            BrokerJson.Require(action.GetProperty("decision").GetString() == "recommend", $"Consent action is not recommended: {id}");
            BrokerJson.Require(action.GetProperty("approval").GetString() == "required-before-apply", $"Consent action has the wrong approval boundary: {id}");
            consentActions.Add(new ConsentAction
            {
                Id = id,
                Title = RequiredString(action, "title"),
                Description = RequiredString(action, "description"),
                Risk = RequiredString(action, "risk"),
                RequiresAdmin = action.GetProperty("requiresAdmin").GetBoolean(),
                ResourceImpact = RequiredString(action, "resourceImpact"),
                Verification = ReadStrings(action, "verification"),
                Rollback = ReadStrings(action, "rollback")
            });
        }

        BrokerJson.Require(consentActions.Count == playbookActionIds.Count, "Action plan and playbook action sets do not match.");
        string signatureKeyId = signatureDocument.RootElement.GetProperty("keyId").GetString() ?? string.Empty;
        BrokerJson.Require(!string.IsNullOrWhiteSpace(signatureKeyId), "Playbook signature key ID is missing.");

        return new PreparedInputs(
            state.InstallationId,
            key.KeyId,
            ApprovalCryptography.Sha256(actionPlanBytes),
            ApprovalCryptography.Sha256(playbookBytes),
            signatureKeyId,
            approvalMode,
            consentActions.ToArray());
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        string? value = element.GetProperty(propertyName).GetString();
        return !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidDataException($"Consent field is empty: {propertyName}");
    }

    private static string[] ReadStrings(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
}
