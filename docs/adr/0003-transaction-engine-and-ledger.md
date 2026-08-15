# ADR-0003: Transaction Engine, approval binding, and ledger

**Status:** Accepted  
**Date:** 2026-08-14

## Context

A Policy Compiler recommendation is not permission to change Windows. Nara needs a deterministic boundary between advice, user consent, execution, verification, and recovery. AI must not cross that boundary by itself.

## Decision

Every executable playbook is declarative, versioned, adapter-specific, signed with ECDSA P-256/SHA-256, and bound to an action plan. It cannot contain a shell command, script, registry path, download URL, credential, or secret. The engine embeds the trusted public key; the development private key is not stored in the repository. The Stage 2 key is explicitly a development trust root and must be replaced by offline production signing before release.

Execution requires a short-lived approval receipt containing SHA-256 hashes of both the exact action-plan file and exact playbook file, plus the approved rule IDs. Changing either input invalidates the receipt. Version 0.1 accepts only `simulation-only` receipts produced by the test harness; this is not treated as real user consent.

The transaction lifecycle is:

1. Verify the existing ledger hash chain.
2. Validate inputs and approval.
3. Precheck all operations.
4. Capture a byte-exact checkpoint.
5. Apply one allowlisted action at a time.
6. Persist and verify the simulated state.
7. Commit, or restore the checkpoint after any failure.
8. Append every stage to a SHA-256-linked JSONL ledger.

Version 0.1 ships only a `fake-windows` adapter. It can modify one explicitly supplied JSON state file and cannot access Windows registry, services, packages, drivers, Defender, or Windows Update.

## Consequences

- An action missing consent is rejected before checkpoint or apply.
- A modified plan, playbook, or ledger fails closed.
- Re-running an already satisfied playbook leaves state and revision unchanged.
- Recovery is deterministic and does not depend on an AI response or network connection.
- A real Windows adapter is forbidden until these behaviors pass and the adapter is tested inside a disposable VM snapshot.
