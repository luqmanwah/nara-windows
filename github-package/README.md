# Nara Windows Deployment

Nara is a post-install, AI-assisted deployment layer for a fresh, official Windows installation. This repository does not distribute Windows, activation tools, credentials, drivers, or OpenAI credentials.

## Current status

Development preview. The ISO overlay removes a conservative allowlist of consumer AppX packages. The Nara package performs hardware profiling, profile selection, a hash-bound approval, three reversible visual settings, and rollback.

## Build

Run from PowerShell:

```powershell
.\build\Build-Release.ps1
```

The result is written to `artifacts/Nara-Deployment-development/` with:

- `ISO-OVERLAY/` — optional overlay for an official Windows installer prepared before reinstall;
- `NARA/` — post-install package published as a GitHub Release asset;
- `release-manifest.json` — SHA-256 for every file.

## Installation sequence

1. Prepare an official Windows installer, optionally adding the published Nara Unattend Pack.
2. Install Windows normally and finish OOBE.
3. Install network drivers and connect to the internet.
4. Open the latest compatible Nara release on GitHub.
5. Download the bootstrap, manifest, and signature assets.
6. Run the bootstrap; it verifies and downloads the selected Nara package.
7. Review the generated plan, then approve the immutable deployment bundle once.

See `docs/SAFETY.md` before testing.
