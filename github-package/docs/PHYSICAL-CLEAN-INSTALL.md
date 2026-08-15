# Nara physical clean-install runbook (development)

This runbook is for a sacrificial or reinstallable test device. Release 0.1.2-development is not production-ready and cannot recover deleted user data, partitions, BitLocker keys, or an unbootable Windows installation.

## Gate A — before erasing Windows

Do not continue until every item is true:

- Important files exist in two verified copies outside the target disk.
- The BitLocker recovery key is available offline when encryption is enabled.
- A stock Microsoft Windows installer without Nara boots successfully.
- A second installer or copied overlay contains Nara Unattend.
- Network and storage drivers for the exact hardware are stored offline.
- `Nara-Offline-0.1.2-development.zip` is stored on external media and its SHA-256 matches the release page.
- The device is connected to AC power.
- Another device can open the GitHub documentation.

Do not delete the OEM recovery partition during the first physical development test.

## Gate B — install official Windows

1. Boot the official Windows installer with the optional Nara Unattend overlay.
2. Select only the intended internal Windows partition. Disconnect unrelated external storage when practical.
3. Complete Windows Setup and OOBE.
4. Confirm desktop, keyboard, display, storage, and Device Manager are usable.
5. Install the exact offline network driver only if Windows has no network connection.

The Unattend overlay removes only the conservative consumer-app allowlist. It does not install Nara Core and must not disable Defender, Windows Update, recovery, servicing, networking, or driver frameworks.

## Gate C — stage Nara

Choose one path:

- Online: download `NaraBootstrap.ps1` from the matching GitHub Release, verify the SHA-256 shown on the release page, then run it from a local PowerShell window. Never use `irm | iex`.
- Offline: extract `Nara-Offline-0.1.2-development.zip`, open `NARA`, and run `PLAN-NARA.cmd`.

Bootstrap verifies the pinned release manifest, deployment ZIP, and every internal file before launching `PLAN-NARA.cmd`. Plan mode is read-only.

## Gate D — review and apply

1. Read `NARA\Logs\hardware-inventory.json` and `proposed-plan.json`.
2. Confirm only the documented reversible action IDs are present.
3. Copy the recovery pack to external media before applying.
4. Run `INSTALL-NARA.cmd` as Administrator.
5. Type the exact hash-bound approval shown by the installer.
6. Reboot and verify sign-in, network, audio, display, Defender, Windows Update, Store, sleep/resume, and recovery.

## Rollback

- Preferred: run `C:\ProgramData\Nara\RESTORE-NARA.cmd` as Administrator.
- Offline fallback: run `RECOVERY\RECOVER-NARA.cmd` from the offline pack.
- Collect diagnostics with `RECOVERY\COLLECT-DIAGNOSTICS.cmd` before reinstalling when possible.

If Windows cannot boot, Nara Recovery Pack is insufficient. Use Windows Recovery Environment or the verified stock Windows installer.

## Acceptance record

Record Windows edition/build, device model class, action-plan hash, applied action IDs, reboot result, rollback result, protected-component status, idle RAM/CPU, and every failure. Do not promote a development build to stable from a single successful device.
