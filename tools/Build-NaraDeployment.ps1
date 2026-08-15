[CmdletBinding()]
param(
    [string]$Version='0.1.0-development',
    [string]$OutputRoot=(Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts')
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$source=Join-Path $projectRoot 'deployment-src'
$destination=Join-Path $OutputRoot ('NARA-{0}' -f $Version)
if (Test-Path -LiteralPath $destination) { throw "Output already exists: $destination" }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Get-ChildItem -LiteralPath $source -Force | Where-Object Name -ne 'Logs' | Copy-Item -Destination $destination -Recurse -Force
New-Item -ItemType Directory -Path (Join-Path $destination 'Logs') -Force | Out-Null

$files=@(Get-ChildItem -LiteralPath $destination -File -Recurse | Sort-Object FullName)
$manifest=[ordered]@{
    schemaVersion='1.0.0'
    packageId='nara-windows-development'
    version=$Version
    builtAtUtc=[DateTime]::UtcNow.ToString('o')
    productionReady=$false
    files=@($files | ForEach-Object {
        [ordered]@{ path=$_.FullName.Substring($destination.Length+1).Replace('\','/'); size=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $destination 'package-manifest.json') -Encoding UTF8
Write-Output $destination
