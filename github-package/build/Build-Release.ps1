[CmdletBinding()]
param(
    [string]$Version='0.1.2-development',
    [string]$OutputRoot=(Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts')
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$projectRoot=Split-Path -Parent $root
$null=& (Join-Path $PSScriptRoot 'Validate-Source.ps1') -Root $root
$output=Join-Path $OutputRoot ('Nara-Release-{0}' -f $Version)
if(Test-Path -LiteralPath $output){throw "Output already exists: $output"}
$stage=Join-Path $output 'stage'; $assets=Join-Path $output 'assets'
$isoOut=Join-Path $stage 'ISO-OVERLAY'; $naraOut=Join-Path $stage 'NARA'; $recoveryOut=Join-Path $stage 'RECOVERY'; $docsOut=Join-Path $stage 'DOCS'
New-Item -ItemType Directory -Path $isoOut,$naraOut,$recoveryOut,$docsOut,$assets -Force | Out-Null
Copy-Item -Path (Join-Path $root 'iso-overlay\*') -Destination $isoOut -Recurse -Force
Copy-Item -Path (Join-Path $projectRoot 'deployment-src\*') -Destination $naraOut -Recurse -Force
if(Test-Path (Join-Path $naraOut 'Logs')){Get-ChildItem (Join-Path $naraOut 'Logs') -File | Remove-Item -Force}
Copy-Item -Path (Join-Path $root 'recovery\*') -Destination $recoveryOut -Recurse -Force
New-Item -ItemType Directory -Path (Join-Path $recoveryOut 'Core') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'deployment-src\Core\Nara.Core.psm1') -Destination (Join-Path $recoveryOut 'Core\Nara.Core.psm1')
Copy-Item -LiteralPath (Join-Path $root 'docs\PHYSICAL-CLEAN-INSTALL.md') -Destination $docsOut
Copy-Item -LiteralPath (Join-Path $root 'docs\OFFLINE-PACK.md') -Destination $docsOut
Copy-Item -LiteralPath (Join-Path $root 'docs\SAFETY.md') -Destination $docsOut
function New-ContentManifest {
    param([string[]]$Roots,[string]$Base,[string]$Destination)
    $files=@($Roots | ForEach-Object { Get-ChildItem -LiteralPath $_ -Recurse -File } | Sort-Object FullName)
    [ordered]@{schemaVersion='1.0.0';version=$Version;channel='development';productionReady=$false;builtAtUtc=[DateTime]::UtcNow.ToString('o');files=@($files|ForEach-Object{[ordered]@{path=$_.FullName.Substring($Base.Length+1).Replace('\','/');size=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})} |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Destination -Encoding UTF8
}
$deploymentManifest=Join-Path $output 'release-manifest.json'
New-ContentManifest -Roots @($naraOut) -Base $stage -Destination $deploymentManifest
$offlineManifest=Join-Path $stage 'release-manifest.json'
New-ContentManifest -Roots @($isoOut,$naraOut,$recoveryOut,$docsOut) -Base $stage -Destination $offlineManifest
$deploymentZip=Join-Path $assets "Nara-Deployment-$Version.zip"; $unattendZip=Join-Path $assets "Nara-Unattend-$Version.zip"; $recoveryZip=Join-Path $assets "Nara-Recovery-$Version.zip"; $offlineZip=Join-Path $assets "Nara-Offline-$Version.zip"
Compress-Archive -Path $naraOut,$deploymentManifest -DestinationPath $deploymentZip -CompressionLevel Optimal
Compress-Archive -Path $isoOut -DestinationPath $unattendZip -CompressionLevel Optimal
Compress-Archive -Path $recoveryOut -DestinationPath $recoveryZip -CompressionLevel Optimal
Compress-Archive -Path $isoOut,$naraOut,$recoveryOut,$docsOut,$offlineManifest -DestinationPath $offlineZip -CompressionLevel Optimal
$releaseAssets=@(@($deploymentZip,$unattendZip,$recoveryZip,$offlineZip) | ForEach-Object {$item=Get-Item -LiteralPath $_;[ordered]@{name=$item.Name;size=$item.Length;sha256=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash}})
$releaseDescriptor=Join-Path $assets 'nara-release.json'
[ordered]@{schemaVersion='1.0.0';releaseId="nara-windows-$Version";version=$Version;tag="v$Version";channel='development';productionReady=$false;repository='luqmanwah/nara-windows';supportedArchitectures=@('x64');testedTargets=@();releaseSigning='not-available-development';assets=$releaseAssets} |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $releaseDescriptor -Encoding UTF8
$descriptorHash=(Get-FileHash -LiteralPath $releaseDescriptor -Algorithm SHA256).Hash
$bootstrapTemplate=Get-Content -LiteralPath (Join-Path $root 'bootstrap\NaraBootstrap.ps1') -Raw
$bootstrap=$bootstrapTemplate.Replace('__NARA_VERSION__',$Version).Replace('__RELEASE_MANIFEST_SHA256__',$descriptorHash)
$bootstrapPath=Join-Path $assets 'NaraBootstrap.ps1'
[IO.File]::WriteAllText($bootstrapPath,$bootstrap,[Text.UTF8Encoding]::new($false))
$checksums=Join-Path $assets 'SHA256SUMS.txt'
Get-ChildItem -LiteralPath $assets -File | Sort-Object Name | ForEach-Object {'{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash,$_.Name} | Set-Content -LiteralPath $checksums -Encoding ascii
Write-Output $assets
