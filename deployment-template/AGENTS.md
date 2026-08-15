# Nara deployment rules

## Objective

Prepare the lightest stable and compatible Nara configuration for this Windows device from the supplied local evidence. Preserve Windows servicing, security, recovery, drivers, activation, and user control.

## Authority

- Reading workspace files, auditing the device, producing reports, validating hashes, and running non-mutating checks are authorized.
- Before any Windows mutation, create `Reports/proposed-plan.json` and wait for one explicit approval of the complete, immutable action bundle and its exact action IDs.
- Never infer approval from the selected profile, update mode, prior chat, or ownership of the device.
- External downloads require user approval and must use an allowlisted official source from `Policy/source-policy.json`.
- Destructive, irreversible, credential-handling, or scope-expanding actions are forbidden.

## Hard safety rules

- Never disable or replace Microsoft Defender in this deployment.
- Never permanently disable Windows Update. Updates may only be deferred or recommended through supported Windows controls.
- Never remove or damage WinSxS, servicing stack, Windows Recovery Environment, activation, boot files, Windows Security, networking, PowerShell, Microsoft Store infrastructure, DirectX, .NET, Visual C++ runtimes, or driver frameworks.
- Never install unsigned drivers or use a driver that does not match a detected Hardware ID.
- Never read, store, export, or automate entry of passwords, cookies, session tokens, ChatGPT credentials, or API keys.
- A ChatGPT subscription is not an API credential. Do not create an API integration unless the user separately opts in to API billing and verification.
- Never claim that a local model is installed, suitable, or functional without a measured resource check and a launch test.
- Never execute newly generated system-tuning code directly. New actions must first become reviewable development playbook entries with rollback and verification.
- In production, Codex may select and parameterize verified packages but may not generate new mutation code on the target device.

## Decision policy

- Prefer signed, versioned, prebuilt packages from `Catalog/package-catalog.json`.
- Select the profile from measured hardware and user needs, not marketing labels.
- On systems below 12 GB RAM, default to no local LLM. Use the on-demand local rule core and online Codex/ChatGPT handoff.
- Keep Nara idle usage near zero: prefer on-demand processes over persistent services.
- Every proposed action requires rationale, risk, expected resource effect, prerequisites, verification, and rollback.
- A single-click run means one bundle approval followed by unattended deterministic execution. It never means silent consent or unconstrained AI execution.
- If evidence is missing or contradictory, stop that action and report the missing evidence; continue with independent safe analysis.

## Required workflow

1. Validate the workspace.
2. Read inventory and report conflicts.
3. Recommend a profile and AI runtime mode.
4. Resolve only compatible catalog packages.
5. Produce a dry-run plan; do not mutate Windows.
6. Present the plan and exact action IDs for approval.
7. After approval, checkpoint affected state immediately before execution.
8. Apply actions transactionally and verify each action.
9. Roll back the transaction if a required verification fails.
10. Produce a deployment report including unchanged protected components.

## Completion criteria

Deployment is complete only when the selected components launch successfully, required verification passes, rollback material exists, protected Windows components remain healthy, and the final report records every applied or skipped action.
