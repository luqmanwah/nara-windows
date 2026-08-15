using Nara.ApprovalContracts;

namespace Nara.PlaybookEngine;

internal static class TransactionEngine
{
    internal const string EngineVersion = "0.3.0";

    internal static ExecutionResult Execute(ExecutionRequest request)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        string transactionId = Guid.NewGuid().ToString();
        byte[] actionPlanBytes = File.ReadAllBytes(Path.GetFullPath(request.ActionPlanPath));
        byte[] playbookBytes = File.ReadAllBytes(Path.GetFullPath(request.PlaybookPath));
        byte[] signatureBytes = File.ReadAllBytes(Path.GetFullPath(request.SignaturePath));
        byte[] trustManifestBytes = File.ReadAllBytes(Path.GetFullPath(request.TrustManifestPath));
        byte[] trustManifestSignatureBytes = File.ReadAllBytes(Path.GetFullPath(request.TrustManifestSignaturePath));
        byte[] approvalBytes = File.ReadAllBytes(Path.GetFullPath(request.ApprovalPath));
        byte[] checkpoint = File.ReadAllBytes(Path.GetFullPath(request.StatePath));
        string actionPlanHash = JsonSupport.Sha256(actionPlanBytes);
        string playbookHash = JsonSupport.Sha256(playbookBytes);
        string stateBeforeHash = JsonSupport.Sha256(checkpoint);

        ActionPlanEvidence plan = InputLoader.LoadActionPlan(actionPlanBytes);
        PlaybookDocument playbook = InputLoader.LoadPlaybook(playbookBytes);
        TrustManifest trustManifest = InputLoader.LoadAndVerifyTrustManifest(
            trustManifestBytes,
            trustManifestSignatureBytes,
            startedAt);
        PlaybookSignature signature = InputLoader.LoadAndVerifySignature(
            signatureBytes,
            playbookBytes,
            trustManifest,
            startedAt);
        FakeWindowsState state = InputLoader.LoadState(checkpoint);
        ApprovalReceiptV3 approval = InputLoader.LoadApproval(approvalBytes, state, startedAt);
        FakeWindowsAdapter adapter = new(request.StatePath);
        LedgerChain ledger = new(request.LedgerPath);
        long ledgerFirstSequence = ledger.NextSequence;
        List<string> appliedActionIds = [];
        List<string> revertedActionIds = [];
        bool changed = false;

        try
        {
            Precheck(
                plan,
                playbook,
                approval,
                state,
                adapter,
                actionPlanHash,
                playbookHash,
                signature.KeyId,
                ledger.HasObservedApproval(approval.ApprovalId),
                request.TestFailAfterAction);

            ledger.Append(
                transactionId,
                approval.ApprovalId,
                "precheck",
                "success",
                actionId: null,
                stateBeforeHash,
                stateBeforeHash,
                "Input hashes, approval scope, allowlist, and current fake state passed precheck.");
        }
        catch (InvalidDataException exception)
        {
            ledger.Append(
                transactionId,
                approval.ApprovalId,
                "precheck",
                "rejected",
                actionId: null,
                stateBeforeHash,
                stateBeforeHash,
                SanitizeMessage(exception.Message));
            TransactionReport rejected = CreateReport(
                transactionId,
                "rejected",
                startedAt,
                actionPlanHash,
                playbookHash,
                signature.KeyId,
                approval.ApprovalId,
                approval.SessionNonce,
                state.InstallationId,
                approval.ConsentRequestSha256,
                changed: false,
                stateBeforeHash,
                stateBeforeHash,
                appliedActionIds,
                revertedActionIds,
                [SanitizeMessage(exception.Message)],
                ledgerFirstSequence,
                ledger.LastSequence);
            JsonSupport.WriteReport(request.ReportPath, rejected);
            return new ExecutionResult("rejected", 1, rejected);
        }

        ledger.Append(
            transactionId,
            approval.ApprovalId,
            "checkpoint",
            "success",
            actionId: null,
            stateBeforeHash,
            stateBeforeHash,
            "Captured a byte-exact checkpoint of the fake Windows state.");

        try
        {
            foreach (PlaybookAction action in playbook.Actions)
            {
                string beforeApplyHash = JsonSupport.Sha256(File.ReadAllBytes(Path.GetFullPath(request.StatePath)));
                bool actionChanged = adapter.Apply(state, action);
                changed |= actionChanged;
                appliedActionIds.Add(action.RuleId);
                string afterApplyHash = JsonSupport.Sha256(File.ReadAllBytes(Path.GetFullPath(request.StatePath)));
                ledger.Append(
                    transactionId,
                    approval.ApprovalId,
                    "apply",
                    "success",
                    action.RuleId,
                    beforeApplyHash,
                    afterApplyHash,
                    actionChanged ? "Applied allowlisted fake settings." : "Settings were already at the desired value; no state write occurred.");

                if (request.TestFailAfterAction == action.RuleId)
                {
                    throw new InvalidOperationException($"Injected simulation failure after {action.RuleId}.");
                }

                adapter.Verify(state, action);
                ledger.Append(
                    transactionId,
                    approval.ApprovalId,
                    "verify",
                    "success",
                    action.RuleId,
                    afterApplyHash,
                    afterApplyHash,
                    "Verified the desired fake state for this action.");
            }

            string stateAfterHash = JsonSupport.Sha256(File.ReadAllBytes(Path.GetFullPath(request.StatePath)));
            ledger.Append(
                transactionId,
                approval.ApprovalId,
                "commit",
                "success",
                actionId: null,
                stateBeforeHash,
                stateAfterHash,
                changed ? "Committed the simulated transaction." : "Committed an idempotent no-op transaction.");

            TransactionReport committed = CreateReport(
                transactionId,
                "committed",
                startedAt,
                actionPlanHash,
                playbookHash,
                signature.KeyId,
                approval.ApprovalId,
                approval.SessionNonce,
                state.InstallationId,
                approval.ConsentRequestSha256,
                changed,
                stateBeforeHash,
                stateAfterHash,
                appliedActionIds,
                revertedActionIds,
                [],
                ledgerFirstSequence,
                ledger.LastSequence);
            JsonSupport.WriteReport(request.ReportPath, committed);
            return new ExecutionResult("committed", 0, committed);
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            string failedStateHash = JsonSupport.Sha256(File.ReadAllBytes(Path.GetFullPath(request.StatePath)));
            List<string> recoveryErrors = [SanitizeMessage(exception.Message)];
            bool ledgerWritable = true;
            TryAppendRecovery(
                ledger,
                ref ledgerWritable,
                () => ledger.Append(
                    transactionId,
                    approval.ApprovalId,
                    "failure",
                    "failed",
                    appliedActionIds.LastOrDefault(),
                    stateBeforeHash,
                    failedStateHash,
                    SanitizeMessage(exception.Message)),
                recoveryErrors);
            TryAppendRecovery(
                ledger,
                ref ledgerWritable,
                () => ledger.Append(
                    transactionId,
                    approval.ApprovalId,
                    "revert",
                    "started",
                    actionId: null,
                    failedStateHash,
                    failedStateHash,
                    "Restoring the byte-exact checkpoint."),
                recoveryErrors);

            adapter.Restore(checkpoint);
            string restoredHash = JsonSupport.Sha256(File.ReadAllBytes(Path.GetFullPath(request.StatePath)));
            if (restoredHash != stateBeforeHash)
            {
                throw new InvalidDataException("Rollback failed to restore the checkpoint hash.");
            }

            revertedActionIds.AddRange(appliedActionIds.AsEnumerable().Reverse());
            TryAppendRecovery(
                ledger,
                ref ledgerWritable,
                () => ledger.Append(
                    transactionId,
                    approval.ApprovalId,
                    "revert",
                    "restored",
                    actionId: null,
                    failedStateHash,
                    restoredHash,
                    "Restored and verified the checkpoint hash."),
                recoveryErrors);

            TransactionReport reverted = CreateReport(
                transactionId,
                "reverted",
                startedAt,
                actionPlanHash,
                playbookHash,
                signature.KeyId,
                approval.ApprovalId,
                approval.SessionNonce,
                state.InstallationId,
                approval.ConsentRequestSha256,
                changed: false,
                stateBeforeHash,
                restoredHash,
                appliedActionIds,
                revertedActionIds,
                recoveryErrors,
                ledgerFirstSequence,
                ledger.LastSequence);
            JsonSupport.WriteReport(request.ReportPath, reverted);
            return new ExecutionResult("reverted", 3, reverted);
        }
    }

    private static void Precheck(
        ActionPlanEvidence plan,
        PlaybookDocument playbook,
        ApprovalReceiptV3 approval,
        FakeWindowsState state,
        FakeWindowsAdapter adapter,
        string actionPlanHash,
        string playbookHash,
        string playbookSignatureKeyId,
        bool approvalAlreadyObserved,
        string? testFailAfterAction)
    {
        JsonSupport.Require(approval.ActionPlanSha256 == actionPlanHash, "Approval does not match the exact action-plan hash.");
        JsonSupport.Require(approval.PlaybookSha256 == playbookHash, "Approval does not match the exact playbook hash.");
        JsonSupport.Require(approval.PlaybookSignatureKeyId == playbookSignatureKeyId, "Approval does not match the verified playbook signing key.");
        JsonSupport.Require(approval.InstallationId == state.InstallationId, "Approval belongs to a different Nara installation.");
        JsonSupport.Require(!approvalAlreadyObserved, "Single-use approval receipt has already been consumed.");
        JsonSupport.Require(state.Adapter == playbook.Adapter, "Playbook and state adapters do not match.");

        HashSet<string> approved = approval.ApprovedActionIds.ToHashSet(StringComparer.Ordinal);
        HashSet<string> playbookActions = playbook.Actions.Select(action => action.RuleId).ToHashSet(StringComparer.Ordinal);
        if (testFailAfterAction is not null)
        {
            JsonSupport.Require(playbookActions.Contains(testFailAfterAction), "Injected failure target is not in the playbook.");
        }

        foreach (PlaybookAction action in playbook.Actions)
        {
            JsonSupport.Require(plan.Actions.TryGetValue(action.RuleId, out PlanActionEvidence? evidence), $"Action is missing from the action plan: {action.RuleId}");
            JsonSupport.Require(evidence.Decision == "recommend", $"Action is not executable in engine 0.3.0: {action.RuleId}");
            JsonSupport.Require(evidence.Approval == "required-before-apply", $"Action does not use the required approval boundary: {action.RuleId}");
            JsonSupport.Require(evidence.Risk == "low", $"Engine 0.3.0 refuses non-low-risk actions: {action.RuleId}");
            JsonSupport.Require(!evidence.RequiresAdmin, $"Engine 0.3.0 refuses Administrator actions: {action.RuleId}");
            JsonSupport.Require(approved.Contains(action.RuleId), $"Action lacks approval: {action.RuleId}");
            foreach (SettingOperation operation in action.Operations)
            {
                adapter.Precheck(state, operation);
            }
        }
    }

    private static TransactionReport CreateReport(
        string transactionId,
        string status,
        DateTimeOffset startedAt,
        string actionPlanHash,
        string playbookHash,
        string playbookSignatureKeyId,
        string approvalId,
        string approvalSessionNonce,
        string installationId,
        string consentRequestSha256,
        bool changed,
        string stateBeforeHash,
        string stateAfterHash,
        IReadOnlyList<string> appliedActionIds,
        IReadOnlyList<string> revertedActionIds,
        IReadOnlyList<string> errors,
        long ledgerFirstSequence,
        long ledgerLastSequence) => new(
            SchemaVersion: "1.0.0",
            EngineVersion: EngineVersion,
            TransactionId: transactionId,
            Simulation: true,
            Status: status,
            StartedAtUtc: JsonSupport.Utc(startedAt),
            FinishedAtUtc: JsonSupport.Utc(DateTimeOffset.UtcNow),
            ActionPlanSha256: actionPlanHash,
            PlaybookSha256: playbookHash,
            PlaybookSignatureKeyId: playbookSignatureKeyId,
            ApprovalId: approvalId,
            ApprovalSessionNonce: approvalSessionNonce,
            InstallationId: installationId,
            ConsentRequestSha256: consentRequestSha256,
            Changed: changed,
            StateBeforeSha256: stateBeforeHash,
            StateAfterSha256: stateAfterHash,
            AppliedActionIds: appliedActionIds.ToArray(),
            RevertedActionIds: revertedActionIds.ToArray(),
            Errors: errors.ToArray(),
            LedgerFirstSequence: ledgerFirstSequence,
            LedgerLastSequence: ledgerLastSequence);

    private static string SanitizeMessage(string message) =>
        string.IsNullOrWhiteSpace(message) ? "Transaction failed without a message." : message.ReplaceLineEndings(" ");

    private static void TryAppendRecovery(
        LedgerChain ledger,
        ref bool ledgerWritable,
        Func<LedgerEvent> append,
        ICollection<string> errors)
    {
        if (!ledgerWritable)
        {
            return;
        }

        try
        {
            _ = append();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ledgerWritable = false;
            errors.Add($"Ledger unavailable during recovery: {SanitizeMessage(exception.Message)}");
        }
    }
}
