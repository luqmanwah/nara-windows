# GitHub-first installation

## Boundary

GitHub distributes Nara and the optional unattend overlay. It does not distribute Microsoft Windows. A true clean installation still needs official Windows installation or recovery media capable of booting before an operating system exists.

## User flow

1. Install official Windows, with the optional Nara Unattend Pack applied while preparing the installer.
2. Finish OOBE, install a network driver if required, and establish HTTPS connectivity.
3. Visit the Nara GitHub Releases page and choose `stable`, `preview`, or `development`.
4. Download `NaraBootstrap.ps1`, `nara-bootstrap-manifest.json`, and its signature.
5. Verify the published signer and execute the downloaded file locally. Do not use a remote-script pipe such as `irm ... | iex`.
6. Bootstrap audits hardware, resolves a compatible release, downloads it to staging, validates hash and signature, and creates a dry-run plan.
7. The user approves the immutable bundle once. The local runner performs checkpoint, apply, reboot/resume, verification, and rollback.

## Trust model

- Branch contents are never installed directly.
- Only immutable GitHub Release assets are eligible.
- A release manifest must declare channel, version, supported builds, architecture, package URL, SHA-256, minimum bootstrap version, and package status.
- Stable bootstrap trusts pinned Nara release-signing public keys, not GitHub transport alone.
- A changed manifest, package, action list, or plan hash invalidates prior approval.
- If verification or compatibility fails, installation stops without mutation.

## Channels

- `stable`: passed the full clean-install and rollback matrix.
- `preview`: release candidate for opted-in testers.
- `development`: experimental devices where reinstall is acceptable.

All physical targets remain on `development` until clean install, reapply, reboot/resume, rollback, update, driver, application, and idle tests pass.
