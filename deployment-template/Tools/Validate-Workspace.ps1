[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$required = @(
    'AGENTS.md', 'START-HERE.md', 'deployment-request.json',
    'Policy\user-policy.json', 'Policy\source-policy.json',
    'Policy\compatibility-policy.json',
    'Profiles\profile-rules.json', 'Catalog\package-catalog.json',
    'Catalog\deployment-phases.json',
    'Playbooks\ultra-lite.development.json'
)
foreach ($relative in $required) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing required file: $relative" }
    if ([IO.Path]::GetExtension($path) -eq '.json') {
        Get-Content -LiteralPath $path -Raw | ConvertFrom-Json | Out-Null
    }
}
$request = Get-Content -LiteralPath (Join-Path $root 'deployment-request.json') -Raw | ConvertFrom-Json
if ($request.phase -ne 'plan-only' -or $request.requiresExplicitApprovalBeforeMutation -ne $true) {
    throw 'Unsafe deployment request defaults.'
}
Write-Output "PASS Nara deployment workspace: $root"
