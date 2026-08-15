# ADR-0002: Policy precedence and dry-run contract

**Status:** Accepted  
**Date:** 2026-08-14

## Context

Nara combines a user-selected profile, detected hardware, approval preferences, security requirements, and versioned Nara policy. Conflicts must be resolved deterministically and must never let an optimization silently weaken mandatory security.

## Decision

The Policy Compiler uses this precedence, from highest to lowest:

1. Mandatory security baseline.
2. Explicit user intent.
3. Selected device profile.
4. Detected hardware capability.
5. Approval mode.
6. Current signed Nara policy version.

Compilation is read-only. It may write only the requested action-plan JSON file. It cannot execute commands, change registry values, modify services, install packages, or approve its own plan.

Profiles are declarative data and cannot contain arbitrary scripts, command lines, registry paths, credentials, tokens, or API keys. Invalid or unsupported values fail closed.

Incomplete hardware evidence is represented as a blocked or unknown action. It is never filled using an AI guess. In `recommended` mode, every future mutation needs explicit user approval. A later `automatic` mode may auto-apply only low-risk, reversible actions allowed by signed policy; security reductions and high-risk actions still require approval.

Each action must carry a stable rule ID, rationale, risk, privilege requirement, approval state, expected resource impact, preconditions, verification, rollback, and evidence.

## Consequences

- The same inputs yield the same ordered actions; timestamps are metadata only.
- Security and compatibility can override a Lite optimization.
- AI may explain or rank an already valid plan but is not the source of enforcement authority.
- The future Playbook Engine consumes only approved plans and must record verification and rollback in its ledger.
