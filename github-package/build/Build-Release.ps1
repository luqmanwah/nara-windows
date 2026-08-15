[CmdletBinding()]
param([string]$Version='0.1.0-development')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$null=& (Join-Path $PSScriptRoot 'Validate-Source.ps1') -Root $root
$projectRoot=Split-Path -Parent $root
$output=Join-Path $root ('artifacts\Nara-Deployment-{0}' -f $Version)
if(Test-Path -LiteralPath $output){throw "Output already exists: $output"}
$isoOut=Join-Path $output 'ISO-OVERLAY'
$naraOut=Join-Path $output 'NARA'
New-Item -ItemType Directory -Path $isoOut,$naraOut -Force | Out-Null
Copy-Item -Path (Join-Path $root 'iso-overlay\*') -Destination $isoOut -Recurse -Force
Copy-Item -Path (Join-Path $projectRoot 'deployment-src\*') -Destination $naraOut -Recurse -Force
if(Test-Path (Join-Path $naraOut 'Logs')){Get-ChildItem (Join-Path $naraOut 'Logs') -File | Remove-Item -Force}
$files=@(Get-ChildItem -LiteralPath $output -Recurse -File | Sort-Object FullName)
[ordered]@{
  schemaVersion='1.0.0'; version=$Version; productionReady=$false; builtAtUtc=[DateTime]::UtcNow.ToString('o');
  files=@($files|ForEach-Object{[ordered]@{path=$_.FullName.Substring($output.Length+1).Replace('\','/');size=$_.Length;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}})
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'release-manifest.json') -Encoding UTF8
Write-Output $output
