[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $projectRoot '.tools\dotnet-10.0.400\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet) -and $env:CI -eq 'true') {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
}
$profilerAssembly = Join-Path $projectRoot 'src\Nara.HardwareProfiler\bin\Release\net10.0-windows\Nara.HardwareProfiler.dll'
$compilerAssembly = Join-Path $projectRoot 'src\Nara.PolicyCompiler\bin\Release\net10.0-windows\Nara.PolicyCompiler.dll'
$brokerAssembly = Join-Path $projectRoot 'src\Nara.ApprovalBroker\bin\Release\net10.0\Nara.ApprovalBroker.dll'
$engineAssembly = Join-Path $projectRoot 'src\Nara.PlaybookEngine\bin\Release\net10.0-windows\Nara.PlaybookEngine.dll'
$profilePath = Join-Path $projectRoot 'profiles\lite-recommended.json'
$playbookPath = Join-Path $projectRoot 'playbooks\lite-safe-settings.json'
$signaturePath = Join-Path $projectRoot 'playbooks\lite-safe-settings.signature.json'
$trustManifestPath = Join-Path $projectRoot 'trust\nara-development-trust.json'
$trustSignaturePath = Join-Path $projectRoot 'trust\nara-development-trust.signature.json'
$revokedTrustPath = Join-Path $projectRoot 'tests\fixtures\revoked-development-trust.json'
$revokedTrustSignaturePath = Join-Path $projectRoot 'tests\fixtures\revoked-development-trust.signature.json'
$rotatedTrustPath = Join-Path $projectRoot 'tests\fixtures\rotated-development-trust.json'
$rotatedTrustSignaturePath = Join-Path $projectRoot 'tests\fixtures\rotated-development-trust.signature.json'
$rotatedPlaybookSignaturePath = Join-Path $projectRoot 'tests\fixtures\lite-safe-settings.rotated.signature.json'
$stateFixture = Join-Path $projectRoot 'tests\fixtures\fake-windows-state.json'
$schemaRoot = Join-Path $projectRoot 'schemas'
$runRoot = Join-Path $projectRoot ("artifacts\playbook-engine-tests\" + [guid]::NewGuid().ToString())
$latestRoot = Join-Path $projectRoot 'artifacts\playbook-engine\latest'
$inventoryPath = Join-Path $runRoot 'inventory.json'
$planPath = Join-Path $runRoot 'action-plan.json'

$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\nuget-packages'
$env:APPDATA = Join-Path $projectRoot '.tools\appdata'
$env:LOCALAPPDATA = Join-Path $projectRoot '.tools\localappdata'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = '0'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = '1'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'

New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
New-Item -ItemType Directory -Path $latestRoot -Force | Out-Null

pwsh -NoProfile -File (Join-Path $projectRoot 'tools\build\Invoke-NaraBuild.ps1') | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Stage 2 build failed.' }

& $dotnet $profilerAssembly --output $inventoryPath | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Inventory generation failed.' }
& $dotnet $compilerAssembly --inventory $inventoryPath --profile $profilePath --output $planPath | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Action plan generation failed.' }

$initializedState = Join-Path $runRoot 'initialized-state.json'
$fakeApprovalKey = Join-Path $runRoot 'fake-approval-key.json'
$consentPath = Join-Path $runRoot 'consent-request.json'
& $dotnet $brokerAssembly init-simulation --state-template $stateFixture --state $initializedState --key $fakeApprovalKey | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Approval Broker simulation initialization failed.' }
& $dotnet $brokerAssembly prepare --plan $planPath --playbook $playbookPath --signature $signaturePath --trust-manifest $trustManifestPath --trust-signature $trustSignaturePath --state $initializedState --key $fakeApprovalKey --output $consentPath | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Consent request preparation failed.' }

function New-SimulationApproval {
    param(
        [Parameter(Mandatory)] [string] $OutputPath,
        [Parameter(Mandatory)] [string[]] $ApprovedActionIds,
        [string] $ConsentPath = $script:consentPath,
        [string] $KeyPath = $script:fakeApprovalKey,
        [ValidateRange(1, 600)] [int] $TtlSeconds = 600
    )

    $requestId = (Get-Content -LiteralPath $ConsentPath -Raw | ConvertFrom-Json).requestId
    & $script:dotnet $script:brokerAssembly issue-simulation --consent $ConsentPath --key $KeyPath --confirm-request $requestId --approve-actions ($ApprovedActionIds -join ',') --ttl-seconds $TtlSeconds --output $OutputPath | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Approval Broker receipt issuance failed: $OutputPath" }
}

function Assert-JsonSchema {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $SchemaName
    )

    $json = Get-Content -LiteralPath $Path -Raw
    $schema = Join-Path $schemaRoot $SchemaName
    if (-not (Test-Json -Json $json -SchemaFile $schema -ErrorAction Stop)) {
        throw "Schema validation failed: $Path"
    }
}

function Assert-Ledger {
    param([Parameter(Mandatory)] [string] $Path)

    foreach ($line in Get-Content -LiteralPath $Path) {
        if (-not [string]::IsNullOrWhiteSpace($line)) {
            if (-not (Test-Json -Json $line -SchemaFile (Join-Path $schemaRoot 'ledger-event.schema.json') -ErrorAction Stop)) {
                throw 'Ledger event schema validation failed.'
            }
        }
    }

    & $dotnet $engineAssembly --verify-ledger $Path | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Ledger hash-chain verification failed.' }
}

Assert-JsonSchema -Path $playbookPath -SchemaName 'playbook.schema.json'
Assert-JsonSchema -Path $signaturePath -SchemaName 'playbook-signature.schema.json'
Assert-JsonSchema -Path $trustManifestPath -SchemaName 'trust-manifest.schema.json'
Assert-JsonSchema -Path $trustSignaturePath -SchemaName 'trust-manifest-signature.schema.json'
Assert-JsonSchema -Path $revokedTrustPath -SchemaName 'trust-manifest.schema.json'
Assert-JsonSchema -Path $revokedTrustSignaturePath -SchemaName 'trust-manifest-signature.schema.json'
Assert-JsonSchema -Path $rotatedTrustPath -SchemaName 'trust-manifest.schema.json'
Assert-JsonSchema -Path $rotatedTrustSignaturePath -SchemaName 'trust-manifest-signature.schema.json'
Assert-JsonSchema -Path $rotatedPlaybookSignaturePath -SchemaName 'playbook-signature.schema.json'
Assert-JsonSchema -Path $stateFixture -SchemaName 'fake-windows-state.schema.json'
Assert-JsonSchema -Path $initializedState -SchemaName 'fake-windows-state.schema.json'
Assert-JsonSchema -Path $fakeApprovalKey -SchemaName 'fake-approval-key.schema.json'
Assert-JsonSchema -Path $consentPath -SchemaName 'consent-request.schema.json'

$revokedBrokerConsent = Join-Path $runRoot 'revoked-broker-consent-request.json'
& $dotnet $brokerAssembly prepare --plan $planPath --playbook $playbookPath --signature $signaturePath --trust-manifest $revokedTrustPath --trust-signature $revokedTrustSignaturePath --state $initializedState --key $fakeApprovalKey --output $revokedBrokerConsent 2>$null | Out-Null
if ($LASTEXITCODE -ne 1 -or (Test-Path -LiteralPath $revokedBrokerConsent)) {
    throw 'Approval Broker presented consent for a revoked playbook signing key.'
}

$consentObject = Get-Content -LiteralPath $consentPath -Raw | ConvertFrom-Json
$wrongChallengeReceipt = Join-Path $runRoot 'wrong-challenge-receipt.json'
& $dotnet $brokerAssembly issue-simulation --consent $consentPath --key $fakeApprovalKey --confirm-request ([guid]::NewGuid().ToString()) --output $wrongChallengeReceipt 2>$null | Out-Null
if ($LASTEXITCODE -ne 1) { throw 'Approval Broker accepted the wrong confirmation challenge.' }

$tamperedConsentPath = Join-Path $runRoot 'tampered-consent-request.json'
$tamperedConsent = Get-Content -LiteralPath $consentPath -Raw | ConvertFrom-Json
$tamperedConsent.actions[0].title = 'Tampered consent title'
[System.IO.File]::WriteAllText($tamperedConsentPath, ($tamperedConsent | ConvertTo-Json -Depth 10), [System.Text.UTF8Encoding]::new($false))
$tamperedConsentReceipt = Join-Path $runRoot 'tampered-consent-receipt.json'
& $dotnet $brokerAssembly issue-simulation --consent $tamperedConsentPath --key $fakeApprovalKey --confirm-request $tamperedConsent.requestId --output $tamperedConsentReceipt 2>$null | Out-Null
if ($LASTEXITCODE -ne 1) { throw 'Approval Broker accepted a modified consent request.' }

$approvedIds = @('NARA-LITE-UI-001', 'NARA-LITE-UI-002', 'NARA-LITE-AI-001')
$successState = Join-Path $runRoot 'success-state.json'
$successApproval = Join-Path $runRoot 'success-approval.json'
$successLedger = Join-Path $runRoot 'success-ledger.jsonl'
$successReport = Join-Path $runRoot 'success-report.json'
Copy-Item -LiteralPath $initializedState -Destination $successState
New-SimulationApproval -OutputPath $successApproval -ApprovedActionIds $approvedIds
Assert-JsonSchema -Path $successApproval -SchemaName 'approval-receipt.schema.json'

& $dotnet $engineAssembly --plan $planPath --playbook $playbookPath --signature $signaturePath --trust-manifest $trustManifestPath --trust-signature $trustSignaturePath --approval $successApproval --state $successState --ledger $successLedger --report $successReport | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Successful transaction scenario failed.' }

Assert-JsonSchema -Path $successState -SchemaName 'fake-windows-state.schema.json'
Assert-JsonSchema -Path $successReport -SchemaName 'transaction-report.schema.json'
Assert-Ledger -Path $successLedger

$firstTransactionEvents = @(Get-Content -LiteralPath $successLedger | ForEach-Object { $_ | ConvertFrom-Json })
$applyEvents = @($firstTransactionEvents | Where-Object stage -eq 'apply')
$fixtureHash = (Get-FileHash -LiteralPath $initializedState -Algorithm SHA256).Hash.ToLowerInvariant()
if ($applyEvents[0].stateBeforeSha256 -ne $fixtureHash) {
    throw 'First apply event does not begin at the checkpoint hash.'
}
for ($index = 1; $index -lt $applyEvents.Count; $index++) {
    if ($applyEvents[$index].stateBeforeSha256 -ne $applyEvents[$index - 1].stateAfterSha256) {
        throw 'Apply event state hashes do not form a continuous transition.'
    }
}

$state = Get-Content -LiteralPath $successState -Raw | ConvertFrom-Json
$report = Get-Content -LiteralPath $successReport -Raw | ConvertFrom-Json
if ($state.settings.uiAnimations -ne $false -or $state.settings.uiTransparency -ne $false) {
    throw 'Committed UI state is incorrect.'
}
if ($state.settings.aiRuntimeMode -ne 'on-demand' -or $state.settings.aiIdleUnloadMinutes -ne 5) {
    throw 'Committed AI lifecycle state is incorrect.'
}
if ($state.revision -ne 3) { throw 'State revision should increase once per changed action.' }
if ($report.status -ne 'committed' -or $report.changed -ne $true -or $report.appliedActionIds.Count -ne 3) {
    throw 'Committed transaction report is incorrect.'
}
if ($report.playbookSignatureKeyId -ne 'nara-stage2-dev-2026') {
    throw 'Committed report does not identify the trusted signing key.'
}
$fakePrivateKey = (Get-Content -LiteralPath $fakeApprovalKey -Raw | ConvertFrom-Json).privateKeyPkcs8Base64
foreach ($publicArtifact in @($consentPath, $successApproval, $successReport, $successLedger)) {
    if ((Get-Content -LiteralPath $publicArtifact -Raw).Contains($fakePrivateKey, [System.StringComparison]::Ordinal)) {
        throw "Simulation private key leaked into a public artifact: $publicArtifact"
    }
}
$parsedNonce = [guid]::Empty
if ($report.installationId -ne $state.installationId -or -not [guid]::TryParse($report.approvalSessionNonce, [ref]$parsedNonce)) {
    throw 'Committed report is not bound to the installation and approval session.'
}
$expectedConsentHash = (Get-FileHash -LiteralPath $consentPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($report.consentRequestSha256 -ne $expectedConsentHash) {
    throw 'Committed report is not bound to the signed consent request.'
}

$stateHashBeforeRepeat = (Get-FileHash -LiteralPath $successState -Algorithm SHA256).Hash
$replayReport = Join-Path $runRoot 'replay-report.json'
& $dotnet $engineAssembly --plan $planPath --playbook $playbookPath --signature $signaturePath --trust-manifest $trustManifestPath --trust-signature $trustSignaturePath --approval $successApproval --state $successState --ledger $successLedger --report $replayReport 2>$null | Out-Null
if ($LASTEXITCODE -ne 1) { throw 'Replayed approval receipt must be rejected.' }
if ($stateHashBeforeRepeat -ne (Get-FileHash -LiteralPath $successState -Algorithm SHA256).Hash) {
    throw 'Replay rejection changed fake state.'
}
$replay = Get-Content -LiteralPath $replayReport -Raw | ConvertFrom-Json
if ($replay.status -ne 'rejected' -or $replay.errors[0] -notmatch 'already been consumed') {
    throw 'Replay rejection report is incorrect.'
}

$repeatReport = Join-Path $runRoot 'repeat-report.json'
$repeatApproval = Join-Path $runRoot 'repeat-approval.json'
New-SimulationApproval -OutputPath $repeatApproval -ApprovedActionIds $approvedIds
& $dotnet $engineAssembly --plan $planPath --playbook $playbookPath --signature $signaturePath --trust-manifest $trustManifestPath --trust-signature $trustSignaturePath --approval $repeatApproval --state $successState --ledger $successLedger --report $repeatReport | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Idempotent repeat scenario failed.' }
$stateHashAfterRepeat = (Get-FileHash -LiteralPath $successState -Algorithm SHA256).Hash
$repeat = Get-Content -LiteralPath $repeatReport -Raw | ConvertFrom-Json
if ($stateHashBeforeRepeat -ne $stateHashAfterRepeat -or $repeat.changed -ne $false) {
    throw 'Repeated playbook changed an already satisfied state.'
}
Assert-Ledger -Path $successLedger

$rollbackState = Join-Path $runRoot 'rollback-state.json'
$rollbackApproval = Join-Path $runRoot 'rollback-approval.json'
$rollbackLedger = Join-Path $runRoot 'rollback-ledger.jsonl'
$rollbackReport = Join-Path $runRoot 'rollback-report.json'
Copy-Item -LiteralPath $initializedState -Destination $rollbackState
New-SimulationApproval -OutputPath $rollbackApproval -ApprovedActionIds $approvedIds
$rollbackHashBefore = (Get-FileHash -LiteralPath $rollbackState -Algorithm SHA256).Hash
& $dotnet $engineAssembly --plan $planPath --playbook $playbookPath --signature $signaturePath --trust-manifest $trustManifestPath --trust-signature $trustSignaturePath --approval $rollbackApproval --state $rollbackState --ledger $rollbackLedger --report $rollbackReport --test-fail-after-action NARA-LITE-UI-001 2>$null | Out-Null
if ($LASTEXITCODE -ne 3) { throw 'Injected failure must return the reverted exit code.' }
$rollbackHashAfter = (Get-FileHash -LiteralPath $rollbackState -Algorithm SHA256).Hash
$rollback = Get-Content -LiteralPath $rollbackReport -Raw | ConvertFrom-Json
if ($rollbackHashBefore -ne $rollbackHashAfter) { throw 'Rollback did not restore the byte-exact checkpoint.' }
if ($rollback.status -ne 'reverted' -or $rollback.revertedActionIds.Count -ne 1) { throw 'Rollback report is incorrect.' }
Assert-JsonSchema -Path $rollbackReport -SchemaName 'transaction-report.schema.json'
Assert-Ledger -Path $rollbackLedger

$unapprovedState = Join-Path $runRoot 'unapproved-state.json'
$unapprovedApproval = Join-Path $runRoot 'unapproved-approval.json'
$unapprovedLedger = Join-Path $runRoot 'unapproved-ledger.jsonl'
$unapprovedReport = Join-Path $runRoot 'unapproved-report.json'
Copy-Item -LiteralPath $initializedState -Destination $unapprovedState
New-SimulationApproval -OutputPath $unapprovedApproval -ApprovedActionIds @('NARA-LITE-UI-001', 'NARA-LITE-UI-002')
$unapprovedHashBefore = (Get-FileHash -LiteralPath $unapprovedState -Algorithm SHA256).Hash
& $dotnet $engineAssembly --plan $planPath --playbook $playbookPath --signature $signaturePath --trust-manifest $trustManifestPath --trust-signature $trustSignaturePath --approval $unapprovedApproval --state $unapprovedState --ledger $unapprovedLedger --report $unapprovedReport 2>$null | Out-Null
if ($LASTEXITCODE -ne 1) { throw 'Missing approval must reject the transaction.' }
if ($unapprovedHashBefore -ne (Get-FileHash -LiteralPath $unapprovedState -Algorithm SHA256).Hash) {
    throw 'Rejected transaction changed fake state.'
}
$unapproved = Get-Content -LiteralPath $unapprovedReport -Raw | ConvertFrom-Json
if ($unapproved.status -ne 'rejected' -or $unapproved.appliedActionIds.Count -ne 0) {
    throw 'Approval rejection report is incorrect.'
}
Assert-Ledger -Path $unapprovedLedger

$changedPlaybook = Join-Path $runRoot 'changed-playbook.json'
$changedPlaybookText = (Get-Content -LiteralPath $playbookPath -Raw) + [Environment]::NewLine
[System.IO.File]::WriteAllText($changedPlaybook, $changedPlaybookText, [System.Text.UTF8Encoding]::new($false))
$hashState = Join-Path $runRoot 'hash-bound-state.json'
$hashLedger = Join-Path $runRoot 'hash-bound-ledger.jsonl'
$hashReport = Join-Path $runRoot 'hash-bound-report.json'
Copy-Item -LiteralPath $initializedState -Destination $hashState
$hashStateBefore = (Get-FileHash -LiteralPath $hashState -Algorithm SHA256).Hash
& $dotnet $engineAssembly --plan $planPath --playbook $changedPlaybook --signature $signaturePath --trust-manifest $trustManifestPath --trust-signature $trustSignaturePath --approval $successApproval --state $hashState --ledger $hashLedger --report $hashReport 2>$null | Out-Null
if ($LASTEXITCODE -ne 1) { throw 'Modified playbook must fail signature verification.' }
if ($hashStateBefore -ne (Get-FileHash -LiteralPath $hashState -Algorithm SHA256).Hash) {
    throw 'Hash-rejected transaction changed fake state.'
}

$wrongHashApproval = Join-Path $runRoot 'wrong-hash-approval.json'
$wrongHashJson = Get-Content -LiteralPath $successApproval -Raw | ConvertFrom-Json
$wrongHashJson.playbookSha256 = '0000000000000000000000000000000000000000000000000000000000000000'
[System.IO.File]::WriteAllText($wrongHashApproval, ($wrongHashJson | ConvertTo-Json -Depth 5), [System.Text.UTF8Encoding]::new($false))
$wrongHashState = Join-Path $runRoot 'wrong-hash-state.json'
$wrongHashLedger = Join-Path $runRoot 'wrong-hash-ledger.jsonl'
$wrongHashReport = Join-Path $runRoot 'wrong-hash-report.json'
Copy-Item -LiteralPath $initializedState -Destination $wrongHashState
$wrongHashStateBefore = (Get-FileHash -LiteralPath $wrongHashState -Algorithm SHA256).Hash
& $dotnet $engineAssembly --plan $planPath --playbook $playbookPath --signature $signaturePath --trust-manifest $trustManifestPath --trust-signature $trustSignaturePath --approval $wrongHashApproval --state $wrongHashState --ledger $wrongHashLedger --report $wrongHashReport 2>$null | Out-Null
if ($LASTEXITCODE -ne 1) { throw 'Approval with the wrong playbook hash must be rejected.' }
if ($wrongHashStateBefore -ne (Get-FileHash -LiteralPath $wrongHashState -Algorithm SHA256).Hash) {
    throw 'Wrong-hash approval changed fake state.'
}

$wrongInstallationApproval = Join-Path $runRoot 'wrong-installation-approval.json'
$otherInitializedState = Join-Path $runRoot 'other-initialized-state.json'
$otherFakeApprovalKey = Join-Path $runRoot 'other-fake-approval-key.json'
$otherConsent = Join-Path $runRoot 'other-consent-request.json'
& $dotnet $brokerAssembly init-simulation --state-template $stateFixture --state $otherInitializedState --key $otherFakeApprovalKey | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Second Approval Broker installation initialization failed.' }
& $dotnet $brokerAssembly prepare --plan $planPath --playbook $playbookPath --signature $signaturePath --trust-manifest $trustManifestPath --trust-signature $trustSignaturePath --state $otherInitializedState --key $otherFakeApprovalKey --output $otherConsent | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Second installation consent preparation failed.' }
New-SimulationApproval -OutputPath $wrongInstallationApproval -ApprovedActionIds $approvedIds -ConsentPath $otherConsent -KeyPath $otherFakeApprovalKey
$wrongInstallationState = Join-Path $runRoot 'wrong-installation-state.json'
$wrongInstallationLedger = Join-Path $runRoot 'wrong-installation-ledger.jsonl'
$wrongInstallationReport = Join-Path $runRoot 'wrong-installation-report.json'
Copy-Item -LiteralPath $initializedState -Destination $wrongInstallationState
$wrongInstallationHashBefore = (Get-FileHash -LiteralPath $wrongInstallationState -Algorithm SHA256).Hash
& $dotnet $engineAssembly --plan $planPath --playbook $playbookPath --signature $signaturePath --trust-manifest $trustManifestPath --trust-signature $trustSignaturePath --approval $wrongInstallationApproval --state $wrongInstallationState --ledger $wrongInstallationLedger --report $wrongInstallationReport 2>$null | Out-Null
if ($LASTEXITCODE -ne 1) { throw 'Approval for another installation must be rejected.' }
if ($wrongInstallationHashBefore -ne (Get-FileHash -LiteralPath $wrongInstallationState -Algorithm SHA256).Hash) {
    throw 'Wrong-installation approval changed fake state.'
}

$expiredApproval = Join-Path $runRoot 'expired-approval.json'
New-SimulationApproval -OutputPath $expiredApproval -ApprovedActionIds $approvedIds -TtlSeconds 1
Start-Sleep -Milliseconds 1500
$expiredState = Join-Path $runRoot 'expired-state.json'
$expiredLedger = Join-Path $runRoot 'expired-ledger.jsonl'
$expiredReport = Join-Path $runRoot 'expired-report.json'
Copy-Item -LiteralPath $initializedState -Destination $expiredState
$expiredHashBefore = (Get-FileHash -LiteralPath $expiredState -Algorithm SHA256).Hash
& $dotnet $engineAssembly --plan $planPath --playbook $playbookPath --signature $signaturePath --trust-manifest $trustManifestPath --trust-signature $trustSignaturePath --approval $expiredApproval --state $expiredState --ledger $expiredLedger --report $expiredReport 2>$null | Out-Null
if ($LASTEXITCODE -ne 1) { throw 'Expired approval receipt must be rejected.' }
if ($expiredHashBefore -ne (Get-FileHash -LiteralPath $expiredState -Algorithm SHA256).Hash) {
    throw 'Expired approval changed fake state.'
}

$revokedApproval = Join-Path $runRoot 'revoked-key-approval.json'
New-SimulationApproval -OutputPath $revokedApproval -ApprovedActionIds $approvedIds
$revokedState = Join-Path $runRoot 'revoked-key-state.json'
$revokedLedger = Join-Path $runRoot 'revoked-key-ledger.jsonl'
$revokedReport = Join-Path $runRoot 'revoked-key-report.json'
Copy-Item -LiteralPath $initializedState -Destination $revokedState
$revokedHashBefore = (Get-FileHash -LiteralPath $revokedState -Algorithm SHA256).Hash
& $dotnet $engineAssembly --plan $planPath --playbook $playbookPath --signature $signaturePath --trust-manifest $revokedTrustPath --trust-signature $revokedTrustSignaturePath --approval $revokedApproval --state $revokedState --ledger $revokedLedger --report $revokedReport 2>$null | Out-Null
if ($LASTEXITCODE -ne 1) { throw 'Revoked playbook signing key must be rejected.' }
if ($revokedHashBefore -ne (Get-FileHash -LiteralPath $revokedState -Algorithm SHA256).Hash) {
    throw 'Revoked-key rejection changed fake state.'
}

$rotatedApproval = Join-Path $runRoot 'rotated-key-approval.json'
$rotatedConsent = Join-Path $runRoot 'rotated-consent-request.json'
& $dotnet $brokerAssembly prepare --plan $planPath --playbook $playbookPath --signature $rotatedPlaybookSignaturePath --trust-manifest $rotatedTrustPath --trust-signature $rotatedTrustSignaturePath --state $initializedState --key $fakeApprovalKey --output $rotatedConsent | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Rotated-key consent preparation failed.' }
New-SimulationApproval -OutputPath $rotatedApproval -ApprovedActionIds $approvedIds -ConsentPath $rotatedConsent
$rotatedState = Join-Path $runRoot 'rotated-key-state.json'
$rotatedLedger = Join-Path $runRoot 'rotated-key-ledger.jsonl'
$rotatedReport = Join-Path $runRoot 'rotated-key-report.json'
Copy-Item -LiteralPath $initializedState -Destination $rotatedState
& $dotnet $engineAssembly --plan $planPath --playbook $playbookPath --signature $rotatedPlaybookSignaturePath --trust-manifest $rotatedTrustPath --trust-signature $rotatedTrustSignaturePath --approval $rotatedApproval --state $rotatedState --ledger $rotatedLedger --report $rotatedReport | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Rotated active playbook key was not accepted.' }
$rotatedReportObject = Get-Content -LiteralPath $rotatedReport -Raw | ConvertFrom-Json
if ($rotatedReportObject.playbookSignatureKeyId -ne 'nara-stage2-rotated-2026') {
    throw 'Rotated transaction report contains the wrong signing key.'
}
Assert-Ledger -Path $rotatedLedger

$retiredApproval = Join-Path $runRoot 'retired-key-approval.json'
New-SimulationApproval -OutputPath $retiredApproval -ApprovedActionIds $approvedIds
$retiredState = Join-Path $runRoot 'retired-key-state.json'
$retiredLedger = Join-Path $runRoot 'retired-key-ledger.jsonl'
$retiredReport = Join-Path $runRoot 'retired-key-report.json'
Copy-Item -LiteralPath $initializedState -Destination $retiredState
$retiredHashBefore = (Get-FileHash -LiteralPath $retiredState -Algorithm SHA256).Hash
& $dotnet $engineAssembly --plan $planPath --playbook $playbookPath --signature $signaturePath --trust-manifest $rotatedTrustPath --trust-signature $rotatedTrustSignaturePath --approval $retiredApproval --state $retiredState --ledger $retiredLedger --report $retiredReport 2>$null | Out-Null
if ($LASTEXITCODE -ne 1) { throw 'Retired playbook key must be rejected for new execution.' }
if ($retiredHashBefore -ne (Get-FileHash -LiteralPath $retiredState -Algorithm SHA256).Hash) {
    throw 'Retired-key rejection changed fake state.'
}

$tamperedLedger = Join-Path $runRoot 'tampered-ledger.jsonl'
$ledgerText = Get-Content -LiteralPath $successLedger -Raw
$tamperedText = $ledgerText.Replace('Committed the simulated transaction.', 'Committed the altered simulated transaction.')
if ($tamperedText -eq $ledgerText) { throw 'Ledger tamper fixture could not be created.' }
[System.IO.File]::WriteAllText($tamperedLedger, $tamperedText, [System.Text.UTF8Encoding]::new($false))
& $dotnet $engineAssembly --verify-ledger $tamperedLedger 2>$null | Out-Null
if ($LASTEXITCODE -ne 4) { throw 'Tampered ledger was not detected.' }

$simulationSource = @(
    (Join-Path $projectRoot 'src\Nara.PlaybookEngine'),
    (Join-Path $projectRoot 'src\Nara.ApprovalBroker'),
    (Join-Path $projectRoot 'src\Nara.ApprovalContracts')
) | ForEach-Object { Get-ChildItem -LiteralPath $_ -Filter '*.cs' } |
    Get-Content -Raw |
    Out-String
$forbiddenApis = @(
    'Microsoft.Win32',
    'ServiceController',
    'System.Diagnostics.Process',
    'ManagementObject',
    'HttpClient',
    'WebClient',
    'UpdateSession',
    'powershell.exe',
    'cmd.exe',
    'dism.exe'
)
foreach ($api in $forbiddenApis) {
    if ($simulationSource.Contains($api, [System.StringComparison]::Ordinal)) {
        throw "Simulation approval or transaction code contains a forbidden Windows or network API: $api"
    }
}

$playbookJson = Get-Content -LiteralPath $playbookPath -Raw
foreach ($property in @('script', 'command', 'registryPath', 'serviceName', 'downloadUrl', 'password', 'token', 'apiKey')) {
    if ($playbookJson -match ('"' + [regex]::Escape($property) + '"\s*:')) {
        throw "Forbidden playbook property detected: $property"
    }
}

Copy-Item -LiteralPath $successState -Destination (Join-Path $latestRoot 'committed-state.json') -Force
Copy-Item -LiteralPath $successReport -Destination (Join-Path $latestRoot 'committed-report.json') -Force
Copy-Item -LiteralPath $successLedger -Destination (Join-Path $latestRoot 'verified-ledger.jsonl') -Force
Copy-Item -LiteralPath $rollbackReport -Destination (Join-Path $latestRoot 'rollback-report.json') -Force
Copy-Item -LiteralPath $replayReport -Destination (Join-Path $latestRoot 'replay-rejection-report.json') -Force
Copy-Item -LiteralPath $rotatedReport -Destination (Join-Path $latestRoot 'rotated-key-report.json') -Force
Copy-Item -LiteralPath $consentPath -Destination (Join-Path $latestRoot 'signed-consent-request.json') -Force
Copy-Item -LiteralPath $successApproval -Destination (Join-Path $latestRoot 'signed-approval-receipt.json') -Force

Write-Output "PASS brokerChallenge=1 consentSignature=1 receiptSignature=1 committed=1 idempotent=1 rollback=1 replayReject=1 expiryReject=1 installationReject=1 rotation=1 retirement=1 revocation=1 hashBinding=1 ledgerTamper=1 revision=$($state.revision)"
