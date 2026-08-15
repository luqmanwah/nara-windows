namespace Nara.PolicyCompiler;

internal static class PolicyCompiler
{
    internal const string CompilerVersion = "0.1.0";

    private static readonly string[] Precedence =
    [
        "mandatory-security",
        "explicit-user-intent",
        "device-profile",
        "hardware-capability",
        "approval-mode",
        "nara-policy-version"
    ];

    internal static ActionPlan Compile(
        ProfileDocument profile,
        InventoryEvidence inventory,
        string inventoryHash,
        string profileHash,
        DateTimeOffset generatedAtUtc)
    {
        List<PlannedAction> actions =
        [
            KeepDefender(),
            RecommendSecurityUpdates(),
            RecommendUi("NARA-LITE-UI-001", "Disable Windows animations", "animations"),
            RecommendUi("NARA-LITE-UI-002", "Disable transparency effects", "transparency"),
            RecommendOnDemandAi(profile.Policies.Ai.IdleUnloadMinutes),
            DeferOptionalPreview(),
            DeferFeatureUpdates(),
            RecommendSafeServicePolicy(),
            EvaluateDriverData(inventory),
            EvaluateMemory(profile, inventory)
        ];

        EnsureStableUniqueActions(actions);

        return new ActionPlan(
            SchemaVersion: "1.0.0",
            CompilerVersion: CompilerVersion,
            GeneratedAtUtc: generatedAtUtc.UtcDateTime.ToString("O"),
            Status: "dry-run",
            Input: new InputHashes(inventoryHash, profileHash),
            DeviceSummary: new DeviceSummary(
                inventory.OsBuild,
                inventory.InstalledRamBytes,
                inventory.GpuCount,
                HardwareDataComplete: inventory.CimAvailable && inventory.GpuCount > 0),
            SelectedProfile: new SelectedProfile(
                profile.ProfileId,
                profile.ProfileVersion,
                profile.DeviceProfile,
                profile.ApprovalMode),
            PolicyPrecedence: Precedence,
            Actions: actions,
            Summary: new ActionSummary(
                Total: actions.Count,
                Keep: actions.Count(action => action.Decision == "keep"),
                Recommend: actions.Count(action => action.Decision == "recommend"),
                Defer: actions.Count(action => action.Decision == "defer"),
                Blocked: actions.Count(action => action.Decision == "blocked"),
                ApprovalRequired: actions.Count(action => action.Approval == "required-before-apply")));
    }

    private static PlannedAction KeepDefender() => new(
        Id: "NARA-SEC-DEF-001",
        Category: "security",
        Title: "Keep Microsoft Defender real-time protection",
        Description: "Preserve the supported Windows malware protection baseline.",
        Decision: "keep",
        Rationale: "Mandatory security has precedence over resource-saving preferences.",
        Risk: "low",
        RequiresAdmin: false,
        Approval: "not-required",
        ResourceImpact: "Keeps the existing security resource cost; no new background agent is added.",
        Preconditions: ["Windows Security components remain available."],
        Verification: ["Confirm real-time protection remains enabled."],
        Rollback: ["No change is planned, so rollback is not required."],
        Evidence: ["profile.policies.security.defenderRealtimeProtection=keep", "precedence=mandatory-security"]);

    private static PlannedAction RecommendSecurityUpdates() => new(
        Id: "NARA-UPD-SEC-001",
        Category: "update",
        Title: "Retain cumulative security updates",
        Description: "Keep cumulative security servicing eligible for installation.",
        Decision: "recommend",
        Rationale: "Lite mode may defer optional features but not remove the security baseline.",
        Risk: "medium",
        RequiresAdmin: true,
        Approval: "required-before-apply",
        ResourceImpact: "Temporary download, installation, and reboot activity; no permanent Nara background process.",
        Preconditions: ["Update is classified as cumulative or security servicing.", "A restore and recovery route is available."],
        Verification: ["Confirm update source and classification.", "Confirm Windows servicing health after reboot."],
        Rollback: ["Use the supported Windows update uninstall or recovery path when Microsoft permits removal."],
        Evidence: ["profile.policies.updates.cumulativeSecurity=recommend", "precedence=mandatory-security"]);

    private static PlannedAction RecommendUi(string id, string title, string setting) => new(
        Id: id,
        Category: "ui",
        Title: title,
        Description: $"Prefer the Lite profile value for {setting}.",
        Decision: "recommend",
        Rationale: "The user selected the Lite profile and the setting is reversible.",
        Risk: "low",
        RequiresAdmin: false,
        Approval: "required-before-apply",
        ResourceImpact: "Small reduction in desktop composition activity.",
        Preconditions: ["The active user has not explicitly overridden this accessibility preference."],
        Verification: [$"Confirm {setting} is off for the active user."],
        Rollback: [$"Restore the previous {setting} preference from the ledger snapshot."],
        Evidence: [$"profile.policies.ui.{setting}=off", "profile.deviceProfile=lite"]);

    private static PlannedAction RecommendOnDemandAi(int idleUnloadMinutes) => new(
        Id: "NARA-LITE-AI-001",
        Category: "ai",
        Title: "Run local AI on demand",
        Description: $"Load the local model only for active work and unload it after {idleUnloadMinutes} idle minutes.",
        Decision: "recommend",
        Rationale: "On-demand loading preserves Lite idle behavior while retaining offline assistance.",
        Risk: "low",
        RequiresAdmin: false,
        Approval: "required-before-apply",
        ResourceImpact: "Near-zero model RAM while idle; model startup latency when first invoked.",
        Preconditions: ["A compatible local runtime and model are selected later."],
        Verification: ["Confirm the model process exits after the configured idle window."],
        Rollback: ["Restore the prior AI runtime and idle-unload settings."],
        Evidence: ["profile.policies.ai.runtimeMode=on-demand", $"profile.policies.ai.idleUnloadMinutes={idleUnloadMinutes}"]);

    private static PlannedAction DeferOptionalPreview() => new(
        Id: "NARA-UPD-OPT-001",
        Category: "update",
        Title: "Defer optional preview updates",
        Description: "Do not prioritize preview-quality updates for a Lite device.",
        Decision: "defer",
        Rationale: "Preview updates are not part of the mandatory security baseline and may introduce extra change.",
        Risk: "low",
        RequiresAdmin: true,
        Approval: "required-before-apply",
        ResourceImpact: "Avoids optional download, servicing, and reboot activity until explicitly requested.",
        Preconditions: ["The update is positively classified as optional or preview."],
        Verification: ["Confirm mandatory cumulative security updates remain eligible."],
        Rollback: ["Restore the previous optional-update preference."],
        Evidence: ["profile.policies.updates.optionalPreview=defer", "profile.deviceProfile=lite"]);

    private static PlannedAction DeferFeatureUpdates() => new(
        Id: "NARA-UPD-FEAT-001",
        Category: "update",
        Title: "Defer Windows feature updates",
        Description: "Hold feature upgrades for compatibility review under the Lite profile.",
        Decision: "defer",
        Rationale: "Feature upgrades can change services and bundled applications, so Lite compatibility is reviewed first.",
        Risk: "medium",
        RequiresAdmin: true,
        Approval: "required-before-apply",
        ResourceImpact: "Avoids large upgrade activity until a compatible release is approved.",
        Preconditions: ["The current Windows release is still within its supported servicing window."],
        Verification: ["Confirm security updates remain available on the current release."],
        Rollback: ["Restore the prior feature-update policy from the ledger."],
        Evidence: ["profile.policies.updates.featureUpdates=defer", "profile.deviceProfile=lite"]);

    private static PlannedAction RecommendSafeServicePolicy() => new(
        Id: "NARA-SVC-001",
        Category: "service",
        Title: "Use safe trigger-start service policy",
        Description: "Evaluate services individually against a signed compatibility allowlist; never disable services in bulk.",
        Decision: "recommend",
        Rationale: "Idle resource savings must not break Windows servicing, drivers, security, networking, or recovery.",
        Risk: "medium",
        RequiresAdmin: true,
        Approval: "required-before-apply",
        ResourceImpact: "Potential idle RAM and process reduction; exact impact requires per-service measurement.",
        Preconditions: ["A signed service rule exists for the exact Windows build.", "Current start type and state are captured."],
        Verification: ["Verify Windows Update, Defender, networking, audio, input, and recovery health."],
        Rollback: ["Restore each service start type and state from the ledger snapshot."],
        Evidence: ["profile.policies.services.strategy=safe-trigger-start", "profile.policies.services.allowBlanketDisable=false"]);

    private static PlannedAction EvaluateDriverData(InventoryEvidence inventory)
    {
        if (inventory.GpuCount == 0 || !inventory.CimAvailable)
        {
            return new PlannedAction(
                Id: "NARA-DRV-UNK-001",
                Category: "driver",
                Title: "Block GPU driver recommendation",
                Description: "Wait for complete GPU inventory before selecting a driver or vendor configuration.",
                Decision: "blocked",
                Rationale: "Compiler 0.1.0 has insufficient hardware evidence and must not guess the GPU.",
                Risk: "high",
                RequiresAdmin: true,
                Approval: "blocked",
                ResourceImpact: "No driver download, installation, or resident vendor software is added.",
                Preconditions: ["Collect GPU identity and current driver version using an approved read-only adapter."],
                Verification: ["Match the detected hardware ID to the official vendor catalog."],
                Rollback: ["No change is planned, so rollback is not required."],
                Evidence: [$"inventory.gpus.count={inventory.GpuCount}", $"inventory.capabilities.cimAvailable={inventory.CimAvailable.ToString().ToLowerInvariant()}"]);
        }

        return new PlannedAction(
            Id: "NARA-DRV-OFF-001",
            Category: "driver",
            Title: "Review official GPU driver",
            Description: "Compare the detected GPU driver with the official vendor release suited to the selected profile.",
            Decision: "recommend",
            Rationale: "Hardware evidence is present, but installation still needs compatibility review and approval.",
            Risk: "medium",
            RequiresAdmin: true,
            Approval: "required-before-apply",
            ResourceImpact: "Driver installation may temporarily use storage, CPU, network, and require a reboot.",
            Preconditions: ["Resolve the package only from the official hardware vendor."],
            Verification: ["Verify publisher signature, hardware match, and post-install GPU health."],
            Rollback: ["Restore the previous signed driver using the Windows driver rollback path."],
            Evidence: [$"inventory.gpus.count={inventory.GpuCount}", "profile.policies.drivers.source=official-vendor"]);
    }

    private static PlannedAction EvaluateMemory(ProfileDocument profile, InventoryEvidence inventory)
    {
        if (inventory.InstalledRamBytes is null)
        {
            return new PlannedAction(
                Id: "NARA-MEM-CAP-001",
                Category: "resource",
                Title: "Block local AI sizing",
                Description: "Wait for a reliable installed-memory reading before selecting a local model tier.",
                Decision: "blocked",
                Rationale: "Resource capacity is unknown.",
                Risk: "high",
                RequiresAdmin: false,
                Approval: "blocked",
                ResourceImpact: "No model is downloaded or loaded.",
                Preconditions: ["Collect physically installed memory with a supported read-only source."],
                Verification: ["Confirm installed memory is at or above the selected profile minimum."],
                Rollback: ["No change is planned, so rollback is not required."],
                Evidence: ["inventory.memory.physicallyInstalledBytes=null"]);
        }

        bool meetsMinimum = inventory.InstalledRamBytes.Value >= profile.MinimumRamBytes;
        return new PlannedAction(
            Id: "NARA-MEM-CAP-001",
            Category: "resource",
            Title: meetsMinimum ? "Allow compact local AI tier" : "Block local AI tier",
            Description: meetsMinimum
                ? "The device meets the Lite profile memory floor; model selection remains on demand."
                : "The device is below the profile memory floor, so local model installation is blocked.",
            Decision: meetsMinimum ? "recommend" : "blocked",
            Rationale: meetsMinimum
                ? "Installed memory is sufficient for the generic 12 GB Lite baseline."
                : "Installing a local model below the declared memory floor would violate the profile.",
            Risk: meetsMinimum ? "low" : "high",
            RequiresAdmin: false,
            Approval: meetsMinimum ? "required-before-apply" : "blocked",
            ResourceImpact: meetsMinimum
                ? "Model RAM is consumed only during active use and released after the idle window."
                : "No model resource is reserved or consumed.",
            Preconditions: ["Benchmark the selected model tier before making it the default."],
            Verification: ["Measure idle RAM before, during, and after local model use."],
            Rollback: ["Remove the model selection and restore the prior local AI configuration."],
            Evidence: [$"inventory.memory.physicallyInstalledBytes={inventory.InstalledRamBytes.Value}", $"profile.minimumRamBytes={profile.MinimumRamBytes}"]);
    }

    private static void EnsureStableUniqueActions(IReadOnlyCollection<PlannedAction> actions)
    {
        if (actions.Select(action => action.Id).Distinct(StringComparer.Ordinal).Count() != actions.Count)
        {
            throw new InvalidOperationException("Policy rule IDs must be unique.");
        }
    }
}
