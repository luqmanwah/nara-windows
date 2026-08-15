[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $projectRoot '.tools\dotnet-10.0.400\dotnet.exe'
$project = Join-Path $projectRoot 'src\Nara.HardwareProfiler\Nara.HardwareProfiler.csproj'
$assembly = Join-Path $projectRoot 'src\Nara.HardwareProfiler\bin\Release\net10.0-windows\Nara.HardwareProfiler.dll'
$schema = Join-Path $projectRoot 'schemas\hardware-inventory.schema.json'
$output = Join-Path $projectRoot 'artifacts\hardware-inventory.csharp.json'
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

& $dotnet restore $project --nologo --ignore-failed-sources --configfile $nugetConfig
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

& $dotnet build $project --configuration Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

& $dotnet $assembly --output $output | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Profiler execution failed.' }

$json = Get-Content -LiteralPath $output -Raw
if (-not (Test-Json -Json $json -SchemaFile $schema -ErrorAction Stop)) {
    throw 'C# inventory does not match hardware-inventory.schema.json.'
}

$inventory = $json | ConvertFrom-Json
if ($inventory.privacy.identifiersCollected -ne $false) {
    throw 'Privacy contract failed.'
}

if ($null -eq $inventory.memory.physicallyInstalledBytes -or $inventory.memory.physicallyInstalledBytes -le 0) {
    throw 'Native memory collection failed.'
}

$forbiddenProperties = @('userName', 'computerName', 'serialNumber', 'token', 'password', 'cookie', 'apiKey')
foreach ($property in $forbiddenProperties) {
    if ($json -match ('"' + [regex]::Escape($property) + '"\s*:')) {
        throw "Forbidden property detected: $property"
    }
}

Write-Output "PASS schema=$($inventory.schemaVersion) collector=$($inventory.collectorVersion) ramBytes=$($inventory.memory.physicallyInstalledBytes)"
