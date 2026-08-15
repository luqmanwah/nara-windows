using System.Security.Cryptography;
using System.Text.Json;
using Nara.ApprovalContracts;

namespace Nara.ApprovalBroker;

internal static class Program
{
    internal static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintHelp(args.Length == 0 ? Console.Error : Console.Out);
            return args.Length == 0 ? 2 : 0;
        }

        try
        {
            string command = args[0];
            Dictionary<string, string> values = ParsePairs(args.Skip(1).ToArray());
            return command switch
            {
                "init-simulation" => InitializeSimulation(values),
                "prepare" => PrepareConsent(values),
                "issue-simulation" => IssueSimulationReceipt(values),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or JsonException
            or CryptographicException)
        {
            Console.Error.WriteLine($"Approval Broker failed safely: {exception.Message}");
            return 1;
        }
    }

    private static int InitializeSimulation(IReadOnlyDictionary<string, string> values)
    {
        RequireOnly(values, "--state-template", "--state", "--key");
        FakeWindowsState template = BrokerJson.ReadStrict<FakeWindowsState>(Required(values, "--state-template"), "Fake-state template");
        (FakeWindowsState state, FakeApprovalKey key) = SimulationKeyStore.Initialize(template, DateTimeOffset.UtcNow);
        BrokerJson.Write(Required(values, "--state"), state);
        BrokerJson.Write(Required(values, "--key"), key);
        Console.Out.WriteLine($"INITIALIZED installation={state.InstallationId} key={key.KeyId} protection={key.Protection}");
        return 0;
    }

    private static int PrepareConsent(IReadOnlyDictionary<string, string> values)
    {
        RequireOnly(values, "--plan", "--playbook", "--signature", "--trust-manifest", "--trust-signature", "--state", "--key", "--output");
        byte[] planBytes = File.ReadAllBytes(Path.GetFullPath(Required(values, "--plan")));
        byte[] playbookBytes = File.ReadAllBytes(Path.GetFullPath(Required(values, "--playbook")));
        byte[] signatureBytes = File.ReadAllBytes(Path.GetFullPath(Required(values, "--signature")));
        byte[] trustManifestBytes = File.ReadAllBytes(Path.GetFullPath(Required(values, "--trust-manifest")));
        byte[] trustSignatureBytes = File.ReadAllBytes(Path.GetFullPath(Required(values, "--trust-signature")));
        FakeWindowsState state = BrokerJson.ReadStrict<FakeWindowsState>(Required(values, "--state"), "Fake Windows state");
        FakeApprovalKey key = BrokerJson.ReadStrict<FakeApprovalKey>(Required(values, "--key"), "Simulation approval key");
        PreparedInputs prepared = ConsentCompiler.Prepare(planBytes, playbookBytes, signatureBytes, state, key);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string verifiedSigningKeyId = BrokerTrustVerifier.Verify(
            playbookBytes,
            signatureBytes,
            trustManifestBytes,
            trustSignatureBytes,
            now);
        BrokerJson.Require(verifiedSigningKeyId == prepared.PlaybookSignatureKeyId, "Broker trust verification returned a different signing key.");
        ConsentRequest request = new()
        {
            SchemaVersion = "1.0.0",
            RequestId = Guid.NewGuid().ToString(),
            InstallationId = prepared.InstallationId,
            BrokerKeyId = prepared.BrokerKeyId,
            Status = "awaiting-user-consent",
            CreatedAtUtc = BrokerJson.Utc(now),
            ExpiresAtUtc = BrokerJson.Utc(now.AddMinutes(10)),
            ActionPlanSha256 = prepared.ActionPlanSha256,
            PlaybookSha256 = prepared.PlaybookSha256,
            PlaybookSignatureKeyId = prepared.PlaybookSignatureKeyId,
            ApprovalMode = prepared.ApprovalMode,
            Actions = prepared.Actions,
            ConsentStatement = "I reviewed these exact actions and request a simulation-only approval receipt.",
            SignatureAlgorithm = "ecdsa-p256-sha256"
        };

        using ECDsa signer = SimulationKeyStore.OpenAndValidate(key, request.InstallationId, request.BrokerKeyId);
        request.RequestSignatureBase64 = ApprovalCryptography.SignConsent(request, signer);
        BrokerJson.Write(Required(values, "--output"), request);
        Console.Out.WriteLine($"CONSENT READY request={request.RequestId} actions={request.Actions.Length} confirm-with-request-id");
        return 0;
    }

    private static int IssueSimulationReceipt(IReadOnlyDictionary<string, string> values)
    {
        RequireOnly(values, "--consent", "--key", "--confirm-request", "--approve-actions", "--ttl-seconds", "--output");
        string consentPath = Required(values, "--consent");
        byte[] consentBytes = File.ReadAllBytes(Path.GetFullPath(consentPath));
        ConsentRequest request = BrokerJson.ReadStrict<ConsentRequest>(consentPath, "Consent request");
        FakeApprovalKey key = BrokerJson.ReadStrict<FakeApprovalKey>(Required(values, "--key"), "Simulation approval key");
        BrokerJson.Require(request.SchemaVersion == "1.0.0", "Unsupported consent-request schema version.");
        BrokerJson.Require(request.Status == "awaiting-user-consent", "Consent request is not awaiting confirmation.");
        BrokerJson.Require(Guid.TryParse(request.RequestId, out _), "Consent request ID is invalid.");
        BrokerJson.Require(request.Actions is not null && request.Actions.Length > 0, "Consent request has no actions.");
        BrokerJson.Require(Required(values, "--confirm-request") == request.RequestId, "Confirmation challenge does not match the consent request ID.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        BrokerJson.Require(DateTimeOffset.TryParse(request.CreatedAtUtc, out DateTimeOffset createdAt), "Consent creation time is invalid.");
        BrokerJson.Require(DateTimeOffset.TryParse(request.ExpiresAtUtc, out DateTimeOffset requestExpires), "Consent expiry time is invalid.");
        BrokerJson.Require(requestExpires > createdAt && now <= requestExpires, "Consent request has expired.");

        using ECDsa signer = SimulationKeyStore.OpenAndValidate(key, request.InstallationId, request.BrokerKeyId);
        BrokerJson.Require(ApprovalCryptography.VerifyConsent(request, key.PublicKeySpkiBase64), "Consent request signature is invalid.");

        HashSet<string> requestedIds = request.Actions.Select(action => action.Id).ToHashSet(StringComparer.Ordinal);
        string[] approvedIds = values.TryGetValue("--approve-actions", out string? selected)
            ? selected.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : request.Actions.Select(action => action.Id).ToArray();
        BrokerJson.Require(approvedIds.Length > 0, "At least one action must be approved.");
        BrokerJson.Require(approvedIds.Distinct(StringComparer.Ordinal).Count() == approvedIds.Length, "Approved action IDs contain duplicates.");
        BrokerJson.Require(approvedIds.All(requestedIds.Contains), "Approval includes an action not shown in the consent request.");
        approvedIds = request.Actions.Select(action => action.Id).Where(approvedIds.Contains).ToArray();

        int ttlSeconds = 600;
        if (values.TryGetValue("--ttl-seconds", out string? ttlText))
        {
            BrokerJson.Require(int.TryParse(ttlText, out ttlSeconds) && ttlSeconds is >= 1 and <= 600, "Receipt TTL must be between 1 and 600 seconds.");
        }
        DateTimeOffset expiresAt = now.AddSeconds(ttlSeconds) < requestExpires ? now.AddSeconds(ttlSeconds) : requestExpires;
        BrokerJson.Require(expiresAt > now, "Receipt would already be expired.");

        ApprovalReceiptV3 receipt = new()
        {
            SchemaVersion = "3.0.0",
            ApprovalId = Guid.NewGuid().ToString(),
            SessionNonce = Guid.NewGuid().ToString(),
            InstallationId = request.InstallationId,
            Scope = "simulation-only",
            ActorType = "local-user-simulation",
            ApprovalMode = request.ApprovalMode,
            ConsentRequestSha256 = ApprovalCryptography.Sha256(consentBytes),
            ActionPlanSha256 = request.ActionPlanSha256,
            PlaybookSha256 = request.PlaybookSha256,
            PlaybookSignatureKeyId = request.PlaybookSignatureKeyId,
            ApprovedActionIds = approvedIds,
            IssuedAtUtc = BrokerJson.Utc(now),
            ExpiresAtUtc = BrokerJson.Utc(expiresAt),
            SingleUse = true,
            ConsentTextVersion = "broker-v1",
            LocalApprovalKeyId = key.KeyId,
            SignatureAlgorithm = "ecdsa-p256-sha256"
        };
        receipt.SignatureBase64 = ApprovalCryptography.SignReceipt(receipt, signer);
        BrokerJson.Write(Required(values, "--output"), receipt);
        Console.Out.WriteLine($"RECEIPT ISSUED approval={receipt.ApprovalId} actions={receipt.ApprovedActionIds.Length} scope={receipt.Scope}");
        return 0;
    }

    private static Dictionary<string, string> ParsePairs(IReadOnlyList<string> args)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        for (int index = 0; index < args.Count; index++)
        {
            string name = args[index];
            if (!name.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                throw new InvalidDataException($"Invalid argument: {name}");
            }
            BrokerJson.Require(values.TryAdd(name, args[++index]), $"Argument was provided more than once: {name}");
        }
        return values;
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException($"Missing required argument: {name}");

    private static void RequireOnly(IReadOnlyDictionary<string, string> values, params string[] allowed)
    {
        foreach (string name in values.Keys)
        {
            BrokerJson.Require(allowed.Contains(name, StringComparer.Ordinal), $"Unsupported argument: {name}");
        }
        foreach (string required in allowed.Where(item => item is not "--approve-actions" and not "--ttl-seconds"))
        {
            _ = Required(values, required);
        }
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp(Console.Error);
        return 2;
    }

    private static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("Nara Approval Broker 0.1.0 — simulation only");
        writer.WriteLine("init-simulation --state-template <json> --state <json> --key <json>");
        writer.WriteLine("prepare --plan <json> --playbook <json> --signature <json> --trust-manifest <json> --trust-signature <json> --state <json> --key <json> --output <consent.json>");
        writer.WriteLine("issue-simulation --consent <json> --key <json> --confirm-request <uuid> [--approve-actions <id,id>] [--ttl-seconds <1-600>] --output <receipt.json>");
        writer.WriteLine("No command issues approval without the exact consent request ID.");
    }
}
