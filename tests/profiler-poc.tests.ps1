[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$collector = Join-Path $projectRoot 'tools\profiler-poc\Collect-NaraHardware.ps1'
$schema = Join-Path $projectRoot 'schemas\hardware-inventory.schema.json'
$output = Join-Path $projectRoot 'artifacts\hardware-inventory.poc.json'

& $collector -OutputPath $output | Out-Null

$json = Get-Content -LiteralPath $output -Raw
$valid = Test-Json -Json $json -SchemaFile $schema -ErrorAction Stop
if (-not $valid) {
    throw 'Hardware inventory does not match the JSON schema.'
}

$inventory = $json | ConvertFrom-Json
if ($inventory.privacy.identifiersCollected -ne $false) {
    throw 'Privacy contract failed: identifiersCollected must remain false.'
}

if ($inventory.platform -ne 'windows') {
    throw 'Platform contract failed.'
}

if ($null -eq $inventory.os.PSObject.Properties['caption']) {
    throw 'OS caption contract failed.'
}

if ($null -eq $inventory.PSObject.Properties['storage']) {
    throw 'Storage inventory contract failed.'
}

Write-Output "PASS schema=$($inventory.schemaVersion) collector=$($inventory.collectorVersion)"
