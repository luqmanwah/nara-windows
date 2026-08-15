[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $projectRoot '.tools\dotnet-10.0.400\dotnet.exe'
$profilerProject = Join-Path $projectRoot 'src\Nara.HardwareProfiler\Nara.HardwareProfiler.csproj'
$profilerAssembly = Join-Path $projectRoot 'src\Nara.HardwareProfiler\bin\Release\net10.0-windows\Nara.HardwareProfiler.dll'
$compilerProject = Join-Path $projectRoot 'src\Nara.PolicyCompiler\Nara.PolicyCompiler.csproj'
$compilerAssembly = Join-Path $projectRoot 'src\Nara.PolicyCompiler\bin\Release\net10.0-windows\Nara.PolicyCompiler.dll'
$inventorySchema = Join-Path $projectRoot 'schemas\hardware-inventory.schema.json'
$profileSchema = Join-Path $projectRoot 'schemas\profile.schema.json'
$planSchema = Join-Path $projectRoot 'schemas\action-plan.schema.json'
$profilePath = Join-Path $projectRoot 'profiles\lite-recommended.json'
$inventoryPath = Join-Path $projectRoot 'artifacts\hardware-inventory.policy-test.json'
$planPath = Join-Path $projectRoot 'artifacts\action-plan.lite-recommended.json'
$invalidProfilePath = Join-Path $projectRoot 'artifacts\invalid-profile.policy-test.json'
$invalidPlanPath = Join-Path $projectRoot 'artifacts\invalid-action-plan.policy-test.json'
$weakenedProfilePath = Join-Path $projectRoot 'artifacts\weakened-security-profile.policy-test.json'
$weakenedPlanPath = Join-Path $projectRoot 'artifacts\weakened-security-plan.policy-test.json'
$repeatPlanPath = Join-Path $projectRoot 'artifacts\action-plan.repeat.policy-test.json'
$nugetConfig = Join-Path $projectRoot 'NuGet.Config'

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

New-Item -ItemType Directory -Path $env:APPDATA -Force | Out-Null
New-Item -ItemType Directory -Path $env:LOCALAPPDATA -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $planPath) -Force | Out-Null

foreach ($project in @($profilerProject, $compilerProject)) {
    & $dotnet restore $project --nologo --ignore-failed-sources --configfile $nugetConfig
    if ($LASTEXITCODE -ne 0) { throw "Restore failed: $project" }

    & $dotnet build $project --configuration Release --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
}

& $dotnet $profilerAssembly --output $inventoryPath | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Hardware inventory generation failed.' }

$inventoryJson = Get-Content -LiteralPath $inventoryPath -Raw
$profileJson = Get-Content -LiteralPath $profilePath -Raw
if (-not (Test-Json -Json $inventoryJson -SchemaFile $inventorySchema -ErrorAction Stop)) {
    throw 'Hardware inventory schema validation failed.'
}
if (-not (Test-Json -Json $profileJson -SchemaFile $profileSchema -ErrorAction Stop)) {
    throw 'Profile schema validation failed.'
}

& $dotnet $compilerAssembly --inventory $inventoryPath --profile $profilePath --output $planPath | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Policy compilation failed.' }

$planJson = Get-Content -LiteralPath $planPath -Raw
if (-not (Test-Json -Json $planJson -SchemaFile $planSchema -ErrorAction Stop)) {
    throw 'Action plan schema validation failed.'
}

$plan = $planJson | ConvertFrom-Json
$inventory = $inventoryJson | ConvertFrom-Json
$profile = $profileJson | ConvertFrom-Json

if ($plan.status -ne 'dry-run') { throw 'Compiler emitted a non-dry-run plan.' }
if ($plan.compilerVersion -ne '0.1.0') { throw 'Unexpected compiler version.' }
if ($plan.selectedProfile.profileId -ne 'lite-recommended') { throw 'Wrong profile selected.' }
if ($plan.selectedProfile.approvalMode -ne 'recommended') { throw 'Wrong approval mode.' }

$expectedPrecedence = @(
    'mandatory-security',
    'explicit-user-intent',
    'device-profile',
    'hardware-capability',
    'approval-mode',
    'nara-policy-version'
)
if (($plan.policyPrecedence -join '|') -ne ($expectedPrecedence -join '|')) {
    throw 'Policy precedence is not stable.'
}

$expectedRuleOrder = @(
    'NARA-SEC-DEF-001',
    'NARA-UPD-SEC-001',
    'NARA-LITE-UI-001',
    'NARA-LITE-UI-002',
    'NARA-LITE-AI-001',
    'NARA-UPD-OPT-001',
    'NARA-UPD-FEAT-001',
    'NARA-SVC-001',
    'NARA-DRV-UNK-001',
    'NARA-MEM-CAP-001'
)
$actualRuleOrder = @($plan.actions | ForEach-Object id)
if (($actualRuleOrder -join '|') -ne ($expectedRuleOrder -join '|')) {
    throw "Golden rule order changed: $($actualRuleOrder -join ', ')"
}
if (($actualRuleOrder | Sort-Object -Unique).Count -ne $actualRuleOrder.Count) {
    throw 'Action rule IDs are not unique.'
}

$defender = $plan.actions | Where-Object id -eq 'NARA-SEC-DEF-001'
if ($defender.decision -ne 'keep' -or $defender.approval -ne 'not-required') {
    throw 'Defender security baseline was weakened.'
}

$optional = $plan.actions | Where-Object id -eq 'NARA-UPD-OPT-001'
$feature = $plan.actions | Where-Object id -eq 'NARA-UPD-FEAT-001'
if ($optional.decision -ne 'defer' -or $feature.decision -ne 'defer') {
    throw 'Lite update filtering changed.'
}

$driver = $plan.actions | Where-Object id -eq 'NARA-DRV-UNK-001'
if ($driver.decision -ne 'blocked' -or $driver.approval -ne 'blocked') {
    throw 'Incomplete GPU data did not fail closed.'
}

$memory = $plan.actions | Where-Object id -eq 'NARA-MEM-CAP-001'
$expectedMemoryDecision = if ($inventory.memory.physicallyInstalledBytes -ge $profile.minimumRamBytes) { 'recommend' } else { 'blocked' }
if ($memory.decision -ne $expectedMemoryDecision) {
    throw 'Memory capability rule does not match inventory evidence.'
}

if ($plan.summary.total -ne $plan.actions.Count) { throw 'Summary total does not match actions.' }
if ($plan.summary.approvalRequired -ne @($plan.actions | Where-Object approval -eq 'required-before-apply').Count) {
    throw 'Approval summary does not match actions.'
}

$expectedInventoryHash = (Get-FileHash -LiteralPath $inventoryPath -Algorithm SHA256).Hash.ToLowerInvariant()
$expectedProfileHash = (Get-FileHash -LiteralPath $profilePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($plan.input.inventorySha256 -ne $expectedInventoryHash -or $plan.input.profileSha256 -ne $expectedProfileHash) {
    throw 'Input provenance hashes are incorrect.'
}

& $dotnet $compilerAssembly --inventory $inventoryPath --profile $profilePath --output $repeatPlanPath | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Repeat policy compilation failed.' }
$repeatPlan = Get-Content -LiteralPath $repeatPlanPath -Raw | ConvertFrom-Json
$firstStableBody = [pscustomobject]@{
    input = $plan.input
    deviceSummary = $plan.deviceSummary
    selectedProfile = $plan.selectedProfile
    policyPrecedence = $plan.policyPrecedence
    actions = $plan.actions
    summary = $plan.summary
} | ConvertTo-Json -Depth 12 -Compress
$repeatStableBody = [pscustomobject]@{
    input = $repeatPlan.input
    deviceSummary = $repeatPlan.deviceSummary
    selectedProfile = $repeatPlan.selectedProfile
    policyPrecedence = $repeatPlan.policyPrecedence
    actions = $repeatPlan.actions
    summary = $repeatPlan.summary
} | ConvertTo-Json -Depth 12 -Compress
if ($firstStableBody -ne $repeatStableBody) {
    throw 'The same inputs did not produce the same action plan body.'
}

function Assert-MemoryTier {
    param(
        [Parameter(Mandatory)] [long] $InstalledBytes,
        [Parameter(Mandatory)] [string] $ExpectedDecision,
        [Parameter(Mandatory)] [string] $CaseName
    )

    $caseInventoryPath = Join-Path $projectRoot "artifacts\hardware-inventory.$CaseName.policy-test.json"
    $casePlanPath = Join-Path $projectRoot "artifacts\action-plan.$CaseName.policy-test.json"
    $caseInventory = $inventoryJson | ConvertFrom-Json
    $caseInventory.memory.physicallyInstalledBytes = $InstalledBytes
    $caseInventoryJson = $caseInventory | ConvertTo-Json -Depth 12
    [System.IO.File]::WriteAllText($caseInventoryPath, $caseInventoryJson, [System.Text.UTF8Encoding]::new($false))

    & $dotnet $compilerAssembly --inventory $caseInventoryPath --profile $profilePath --output $casePlanPath | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Memory boundary compilation failed: $CaseName" }

    $casePlan = Get-Content -LiteralPath $casePlanPath -Raw | ConvertFrom-Json
    $caseMemory = $casePlan.actions | Where-Object id -eq 'NARA-MEM-CAP-001'
    if ($caseMemory.decision -ne $ExpectedDecision) {
        throw "Unexpected memory decision for ${CaseName}: $($caseMemory.decision)"
    }
}

Assert-MemoryTier -InstalledBytes 12884901888 -ExpectedDecision 'recommend' -CaseName '12gb'
Assert-MemoryTier -InstalledBytes 11811160064 -ExpectedDecision 'blocked' -CaseName '11gb'
Assert-MemoryTier -InstalledBytes 68719476736 -ExpectedDecision 'recommend' -CaseName '64gb'

$forbiddenProperties = @('script', 'command', 'registryPath', 'password', 'token', 'cookie', 'apiKey')
foreach ($property in $forbiddenProperties) {
    if ($profileJson -match ('"' + [regex]::Escape($property) + '"\s*:')) {
        throw "Forbidden profile property detected: $property"
    }
}

$compilerSource = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\Nara.PolicyCompiler') -Filter '*.cs' |
    Get-Content -Raw |
    Out-String
$forbiddenCompilerApis = @(
    'Microsoft.Win32',
    'System.Diagnostics.Process',
    'ServiceController',
    'HttpClient',
    'WebClient',
    'powershell.exe',
    'cmd.exe'
)
foreach ($api in $forbiddenCompilerApis) {
    if ($compilerSource.Contains($api, [System.StringComparison]::Ordinal)) {
        throw "Policy Compiler contains a forbidden system or network API: $api"
    }
}

$invalidProfile = $profileJson | ConvertFrom-Json
$invalidProfile | Add-Member -NotePropertyName command -NotePropertyValue 'unsupported'
$invalidProfileJson = $invalidProfile | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($invalidProfilePath, $invalidProfileJson, [System.Text.UTF8Encoding]::new($false))

if (Test-Json -Json $invalidProfileJson -SchemaFile $profileSchema -ErrorAction SilentlyContinue) {
    throw 'Profile schema accepted an arbitrary command field.'
}

& $dotnet $compilerAssembly --inventory $inventoryPath --profile $invalidProfilePath --output $invalidPlanPath 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) {
    throw 'Compiler did not fail closed on an unsupported profile field.'
}

$weakenedProfile = $profileJson | ConvertFrom-Json
$weakenedProfile.policies.security.defenderRealtimeProtection = 'disable'
$weakenedProfileJson = $weakenedProfile | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($weakenedProfilePath, $weakenedProfileJson, [System.Text.UTF8Encoding]::new($false))

if (Test-Json -Json $weakenedProfileJson -SchemaFile $profileSchema -ErrorAction SilentlyContinue) {
    throw 'Profile schema accepted weakened Defender protection.'
}

& $dotnet $compilerAssembly --inventory $inventoryPath --profile $weakenedProfilePath --output $weakenedPlanPath 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) {
    throw 'Compiler accepted weakened Defender protection.'
}

& $dotnet $compilerAssembly --unknown 2>$null | Out-Null
if ($LASTEXITCODE -ne 2) {
    throw 'Unknown CLI arguments must return exit code 2.'
}

Write-Output "PASS profile=$($plan.selectedProfile.profileId) rules=$($plan.actions.Count) approvals=$($plan.summary.approvalRequired) blocked=$($plan.summary.blocked) memory=11/12/64GB"
