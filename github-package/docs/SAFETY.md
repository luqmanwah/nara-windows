# Safety contract

The ISO overlay may remove only package names present in `catalog/consumer-apps.json`.

It must not disable or remove Microsoft Defender, Windows Update, servicing, recovery, activation, networking, driver frameworks, PowerShell, Microsoft Store infrastructure, DirectX, .NET, or Visual C++ runtimes. It must not partition disks, inject credentials, bypass activation, or collect identifiers.

Nara production packages must be immutable and hash-verified. New Windows mutations require a stable action ID, supported-build evidence, checkpoint, verification, and rollback. Development packages must not claim production readiness.
