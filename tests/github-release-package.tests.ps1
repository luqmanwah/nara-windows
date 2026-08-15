[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$testRoot=Join-Path $projectRoot ('artifacts\release-package-tests\' + [guid]::NewGuid().ToString('N'))
$assets=& (Join-Path $projectRoot 'github-package\build\Build-Release.ps1') -Version '0.1.2-development' -OutputRoot $testRoot
if (-not (Test-Path -LiteralPath $assets)) { throw 'Release assets directory was not created.' }
$required=@('NaraBootstrap.ps1','Nara-Deployment-0.1.2-development.zip','Nara-Unattend-0.1.2-development.zip','Nara-Recovery-0.1.2-development.zip','Nara-Offline-0.1.2-development.zip','nara-release.json','SHA256SUMS.txt')
foreach($name in $required){if(-not(Test-Path -LiteralPath (Join-Path $assets $name))){throw "Missing release asset: $name"}}
$descriptor=Get-Content -LiteralPath (Join-Path $assets 'nara-release.json') -Raw | ConvertFrom-Json
if($descriptor.assets.Count -ne 4 -or $descriptor.productionReady -ne $false){throw 'Release descriptor contract failed.'}
foreach($asset in $descriptor.assets){$path=Join-Path $assets $asset.name;if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $asset.sha256){throw "Descriptor hash mismatch: $($asset.name)"}}
$bootstrap=Get-Content -LiteralPath (Join-Path $assets 'NaraBootstrap.ps1') -Raw
if($bootstrap.Contains('__NARA_VERSION__') -or $bootstrap.Contains('__RELEASE_MANIFEST_SHA256__')){throw 'Bootstrap placeholders were not bound.'}
$expanded=Join-Path $testRoot 'expanded-deployment'
Expand-Archive -LiteralPath (Join-Path $assets 'Nara-Deployment-0.1.2-development.zip') -DestinationPath $expanded
$manifest=Get-Content -LiteralPath (Join-Path $expanded 'release-manifest.json') -Raw | ConvertFrom-Json
foreach($file in $manifest.files){$path=Join-Path $expanded ($file.path -replace '/','\');if(-not(Test-Path -LiteralPath $path)){throw "Deployment file missing: $($file.path)"};if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $file.sha256){throw "Internal hash mismatch: $($file.path)"}}
$offline=Join-Path $testRoot 'expanded-offline'
Expand-Archive -LiteralPath (Join-Path $assets 'Nara-Offline-0.1.2-development.zip') -DestinationPath $offline
foreach($path in @('NARA\PLAN-NARA.cmd','NARA\INSTALL-NARA.cmd','RECOVERY\RECOVER-NARA.cmd','RECOVERY\COLLECT-DIAGNOSTICS.cmd','DOCS\PHYSICAL-CLEAN-INSTALL.md','ISO-OVERLAY\autounattend.xml')){if(-not(Test-Path -LiteralPath (Join-Path $offline $path))){throw "Offline pack missing: $path"}}
Write-Output "PASS assets=$($required.Count) descriptorAssets=$($descriptor.assets.Count) internalFiles=$($manifest.files.Count)"
