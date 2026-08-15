[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$publishScript = Join-Path $projectRoot 'tools\build\Publish-HardwareProfiler.ps1'
$dotnet = Join-Path $projectRoot '.tools\dotnet-10.0.400\dotnet.exe'
$launchArtifact = Join-Path $projectRoot 'artifacts\publish\hardware-profiler\framework-dependent\Nara.HardwareProfiler.dll'
$output = Join-Path $projectRoot 'artifacts\hardware-inventory.published.json'
$schema = Join-Path $projectRoot 'schemas\hardware-inventory.schema.json'

$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

& $publishScript -Mode FrameworkDependent
if ($LASTEXITCODE -ne 0) { throw 'Publish script failed.' }

& $dotnet $launchArtifact --output $output | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Published executable failed.' }

$json = Get-Content -LiteralPath $output -Raw
if (-not (Test-Json -Json $json -SchemaFile $schema -ErrorAction Stop)) {
    throw 'Published inventory does not match schema.'
}

$inventory = $json | ConvertFrom-Json
$artifactSize = (Get-Item -LiteralPath $launchArtifact).Length

Write-Output "PASS artifactBytes=$artifactSize schema=$($inventory.schemaVersion) collector=$($inventory.collectorVersion)"
