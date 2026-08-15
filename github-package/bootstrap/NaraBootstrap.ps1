[CmdletBinding()]
param(
    [string]$Version = '__NARA_VERSION__',
    [string]$Repository = 'luqmanwah/nara-windows',
    [string]$StagingRoot = (Join-Path $env:ProgramData 'Nara\Staging'),
    [switch]$DownloadOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$expectedReleaseManifestHash = '__RELEASE_MANIFEST_SHA256__'
if ($expectedReleaseManifestHash -notmatch '^[A-F0-9]{64}$') {
    throw 'Bootstrap template is not release-bound. Download the bootstrap from a Nara GitHub Release.'
}
if ($Repository -ne 'luqmanwah/nara-windows') { throw 'Repository override is not allowed by this development bootstrap.' }
if ($Version -notmatch '^0\.1\.2-development$') { throw "Unsupported bootstrap version binding: $Version" }

$tag = 'v' + $Version
$releaseBase = "https://github.com/$Repository/releases/download/$tag"
$stage = Join-Path $StagingRoot $Version
$manifestPath = Join-Path $stage 'nara-release.json'
$packagePath = Join-Path $stage "Nara-Deployment-$Version.zip"
$expanded = Join-Path $stage 'expanded'

if (Test-Path -LiteralPath $stage) { throw "Staging already exists: $stage. Inspect or remove it manually before retrying." }
New-Item -ItemType Directory -Path $stage -Force | Out-Null

function Get-VerifiedDownload {
    param([Parameter(Mandatory)][string]$Uri,[Parameter(Mandatory)][string]$Destination,[Parameter(Mandatory)][string]$ExpectedSha256)
    if (-not $Uri.StartsWith($releaseBase + '/', [System.StringComparison]::Ordinal)) { throw "Download URL is outside the pinned release: $Uri" }
    Invoke-WebRequest -UseBasicParsing -Uri $Uri -OutFile $Destination
    $actual = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
    if ($actual -cne $ExpectedSha256) { throw "SHA-256 mismatch for $(Split-Path -Leaf $Destination). Expected $ExpectedSha256, actual $actual" }
}

Get-VerifiedDownload -Uri "$releaseBase/nara-release.json" -Destination $manifestPath -ExpectedSha256 $expectedReleaseManifestHash
$release = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($release.schemaVersion -ne '1.0.0' -or $release.version -ne $Version -or $release.channel -ne 'development' -or $release.productionReady -ne $false) {
    throw 'Release manifest identity or channel is invalid.'
}
$deployment = $release.assets | Where-Object name -eq "Nara-Deployment-$Version.zip"
if ($null -eq $deployment -or $deployment.sha256 -notmatch '^[A-F0-9]{64}$') { throw 'Deployment asset is missing or invalid.' }
Get-VerifiedDownload -Uri "$releaseBase/$($deployment.name)" -Destination $packagePath -ExpectedSha256 $deployment.sha256

New-Item -ItemType Directory -Path $expanded -Force | Out-Null
Expand-Archive -LiteralPath $packagePath -DestinationPath $expanded
$contentManifestPath = Join-Path $expanded 'release-manifest.json'
if (-not (Test-Path -LiteralPath $contentManifestPath)) { throw 'Internal release-manifest.json is missing.' }
$contentManifest = Get-Content -LiteralPath $contentManifestPath -Raw | ConvertFrom-Json
foreach ($file in $contentManifest.files) {
    $candidate = [IO.Path]::GetFullPath((Join-Path $expanded ($file.path -replace '/', '\')))
    if (-not $candidate.StartsWith([IO.Path]::GetFullPath($expanded) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Manifest path escaped staging: $($file.path)" }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Manifest file is missing: $($file.path)" }
    if ((Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash -cne $file.sha256) { throw "Internal file hash mismatch: $($file.path)" }
}

$naraRoot = Join-Path $expanded 'NARA'
if (-not (Test-Path -LiteralPath (Join-Path $naraRoot 'PLAN-NARA.cmd'))) { throw 'PLAN-NARA.cmd is missing.' }
Write-Host "Verified Nara development package staged at: $naraRoot" -ForegroundColor Green
if ($DownloadOnly) { exit 0 }
Write-Host 'Launching read-only plan. No Windows setting is changed by this step.' -ForegroundColor Cyan
& (Join-Path $naraRoot 'PLAN-NARA.cmd')
exit $LASTEXITCODE
