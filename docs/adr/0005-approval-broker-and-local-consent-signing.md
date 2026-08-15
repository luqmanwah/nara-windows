# ADR-0005: Approval Broker and local consent signing

**Status:** Accepted for development simulation  
**Date:** 2026-08-14

## Context

An AI recommendation, action plan, or valid Nara playbook is not user consent. The mutation boundary needs an independently verifiable artifact that proves which exact actions were shown, which installation approved them, and whether that approval is still valid. The AI process must not possess or directly invoke the signing authority.

## Decision

Nara introduces a separate Approval Broker. Before it prepares consent, the broker verifies the root-signed trust manifest, its validity window, the release-key status, the exact playbook hash, and the ECDSA playbook signature. A revoked, retired, unknown, expired, or invalid signing key fails closed and produces no consent request.

The broker compiles only the intersection of the dry-run action plan and declarative playbook. It emits a signed Consent Request containing:

- a random request ID and local installation ID;
- hashes of the exact action plan and playbook;
- the verified release signing-key ID;
- the exact action IDs and user-facing risk, resource impact, verification, and rollback details;
- creation and expiry times;
- the local broker-key ID and broker signature.

Receipt issuance requires the exact request ID as an explicit confirmation challenge. The broker verifies the Consent Request signature and limits selected actions to those displayed. Approval Receipt v3 binds the consent-file hash, plan hash, playbook hash, release-key ID, installation ID, approved actions, short TTL, single-use flag, local key ID, and local signature.

The Playbook Engine independently verifies the trust chain, receipt signature, hashes, installation, expiry, action allowlist, and replay state. The broker does not execute actions and the engine cannot mint approval.

## Simulation boundary

Version 0.1.0 is simulation-only. Its ECDSA P-256 private key is deliberately marked `test-only-plaintext`, exists only under ignored test artifacts, and must never ship. The challenge is exercised by a command-line test harness and is not yet proof of a physical user interaction.

The production adapter must create a non-exportable per-installation key using Windows CNG, protect associated metadata with DPAPI where needed, place the replay ledger at a fixed protected location, and expose signing only through a trusted local consent UI. These changes must be implemented and attacked in a disposable Windows VM. AI, plugins, background services, and remote callers must not receive a signing handle or bypass the visible consent gesture.

## Consequences

- A valid but revoked playbook is rejected before it can be presented as consent.
- Editing the consent, receipt, plan, playbook, action selection, or installation binding invalidates execution.
- Wrong challenges, expired requests or receipts, cross-installation receipts, and replayed approval IDs are rejected.
- The simulation validates the protocol and contracts, not the security of production key storage or the authenticity of a real UI click.
- CNG/DPAPI integration, trusted consent UI, protected ledger storage, and production root/release key ceremony remain mandatory release gates.
