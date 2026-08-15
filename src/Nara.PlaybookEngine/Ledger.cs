using System.Text;
using System.Text.Json;

namespace Nara.PlaybookEngine;

internal sealed record LedgerEventUnsigned(
    string SchemaVersion,
    string EventId,
    string TransactionId,
    string ApprovalId,
    long Sequence,
    string RecordedAtUtc,
    string Stage,
    string Outcome,
    string? ActionId,
    string? StateBeforeSha256,
    string? StateAfterSha256,
    string PreviousEventSha256,
    string Message);

internal sealed record LedgerEvent(
    string SchemaVersion,
    string EventId,
    string TransactionId,
    string ApprovalId,
    long Sequence,
    string RecordedAtUtc,
    string Stage,
    string Outcome,
    string? ActionId,
    string? StateBeforeSha256,
    string? StateAfterSha256,
    string PreviousEventSha256,
    string Message,
    string EventSha256);

internal sealed class LedgerIntegrityException(string message) : Exception(message);

internal sealed class LedgerChain
{
    private const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";
    private readonly string path;
    private readonly HashSet<string> observedApprovalIds;
    private string previousHash = GenesisHash;

    internal LedgerChain(string path)
    {
        this.path = Path.GetFullPath(path);
        LastSequence = VerifyExisting(this.path, out previousHash, out observedApprovalIds);
    }

    internal long LastSequence { get; private set; }
    internal long NextSequence => LastSequence + 1;
    internal bool HasObservedApproval(string approvalId) => observedApprovalIds.Contains(approvalId);

    internal LedgerEvent Append(
        string transactionId,
        string approvalId,
        string stage,
        string outcome,
        string? actionId,
        string? stateBeforeSha256,
        string? stateAfterSha256,
        string message)
    {
        ValidateStage(stage, outcome);
        LedgerEventUnsigned unsignedEvent = new(
            SchemaVersion: "1.0.0",
            EventId: Guid.NewGuid().ToString(),
            TransactionId: transactionId,
            ApprovalId: approvalId,
            Sequence: NextSequence,
            RecordedAtUtc: JsonSupport.Utc(DateTimeOffset.UtcNow),
            Stage: stage,
            Outcome: outcome,
            ActionId: actionId,
            StateBeforeSha256: stateBeforeSha256,
            StateAfterSha256: stateAfterSha256,
            PreviousEventSha256: previousHash,
            Message: message);

        string eventHash = HashUnsigned(unsignedEvent);
        LedgerEvent ledgerEvent = ToSigned(unsignedEvent, eventHash);
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.AppendAllText(
            path,
            JsonSupport.SerializeCompact(ledgerEvent) + Environment.NewLine,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        previousHash = eventHash;
        observedApprovalIds.Add(approvalId);
        LastSequence = ledgerEvent.Sequence;
        return ledgerEvent;
    }

    internal static long Verify(string path) => VerifyExisting(Path.GetFullPath(path), out _, out _);

    private static long VerifyExisting(string path, out string lastHash, out HashSet<string> approvalIds)
    {
        lastHash = GenesisHash;
        approvalIds = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return 0;
        }

        long expectedSequence = 1;
        foreach (string line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            LedgerEvent ledgerEvent;
            try
            {
                ledgerEvent = JsonSerializer.Deserialize<LedgerEvent>(line, JsonSupport.StrictOptions)
                    ?? throw new LedgerIntegrityException("Ledger contains an empty event.");
            }
            catch (JsonException exception)
            {
                throw new LedgerIntegrityException($"Ledger JSON is invalid at sequence {expectedSequence}: {exception.Message}");
            }

            if (ledgerEvent.SchemaVersion != "1.0.0")
            {
                throw new LedgerIntegrityException($"Unsupported ledger schema at sequence {expectedSequence}.");
            }
            if (ledgerEvent.Sequence != expectedSequence)
            {
                throw new LedgerIntegrityException($"Ledger sequence mismatch at {expectedSequence}.");
            }
            if (ledgerEvent.PreviousEventSha256 != lastHash)
            {
                throw new LedgerIntegrityException($"Ledger chain is broken at sequence {expectedSequence}.");
            }
            if (!Guid.TryParse(ledgerEvent.EventId, out _)
                || !Guid.TryParse(ledgerEvent.TransactionId, out _)
                || !Guid.TryParse(ledgerEvent.ApprovalId, out _))
            {
                throw new LedgerIntegrityException($"Ledger identifier is invalid at sequence {expectedSequence}.");
            }

            LedgerEventUnsigned unsignedEvent = ToUnsigned(ledgerEvent);
            string expectedHash = HashUnsigned(unsignedEvent);
            if (ledgerEvent.EventSha256 != expectedHash)
            {
                throw new LedgerIntegrityException($"Ledger event hash is invalid at sequence {expectedSequence}.");
            }

            ValidateStage(ledgerEvent.Stage, ledgerEvent.Outcome);
            approvalIds.Add(ledgerEvent.ApprovalId);
            lastHash = ledgerEvent.EventSha256;
            expectedSequence++;
        }

        return expectedSequence - 1;
    }

    private static string HashUnsigned(LedgerEventUnsigned ledgerEvent) =>
        JsonSupport.Sha256(Encoding.UTF8.GetBytes(JsonSupport.SerializeCompact(ledgerEvent)));

    private static LedgerEvent ToSigned(LedgerEventUnsigned item, string eventHash) => new(
        item.SchemaVersion,
        item.EventId,
        item.TransactionId,
        item.ApprovalId,
        item.Sequence,
        item.RecordedAtUtc,
        item.Stage,
        item.Outcome,
        item.ActionId,
        item.StateBeforeSha256,
        item.StateAfterSha256,
        item.PreviousEventSha256,
        item.Message,
        eventHash);

    private static LedgerEventUnsigned ToUnsigned(LedgerEvent item) => new(
        item.SchemaVersion,
        item.EventId,
        item.TransactionId,
        item.ApprovalId,
        item.Sequence,
        item.RecordedAtUtc,
        item.Stage,
        item.Outcome,
        item.ActionId,
        item.StateBeforeSha256,
        item.StateAfterSha256,
        item.PreviousEventSha256,
        item.Message);

    private static void ValidateStage(string stage, string outcome)
    {
        string[] stages = ["precheck", "checkpoint", "apply", "verify", "commit", "failure", "revert"];
        string[] outcomes = ["success", "rejected", "failed", "started", "restored"];
        if (!stages.Contains(stage, StringComparer.Ordinal) || !outcomes.Contains(outcome, StringComparer.Ordinal))
        {
            throw new LedgerIntegrityException("Ledger contains an unsupported stage or outcome.");
        }
    }
}
