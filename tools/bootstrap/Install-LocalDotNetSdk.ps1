[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$sdkVersion = '10.0.400'
$downloadUrl = 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.400/dotnet-sdk-10.0.400-win-x64.zip'
$expectedSha512 = '9b8b88590e4da131bfd0da7aa089d0fc04d5418d5f8607ec13d55dc5a17b4399afd54d496c12657fa05c6c6546dc5eab930f26ac6c50f2d3a7712c0fb378c366'

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$cacheDirectory = Join-Path $projectRoot 'artifacts\bootstrap'
$archivePath = Join-Path $cacheDirectory "dotnet-sdk-$sdkVersion-win-x64.zip"
$installDirectory = Join-Path $projectRoot ".tools\dotnet-$sdkVersion"
$dotnetPath = Join-Path $installDirectory 'dotnet.exe'

if (Test-Path -LiteralPath $dotnetPath) {
    $installedVersion = & $dotnetPath --version
    if ($installedVersion -eq $sdkVersion) {
        Write-Output "READY version=$installedVersion path=$dotnetPath"
        exit 0
    }

    throw "Unexpected SDK found at the version-specific install path: $installedVersion"
}

New-Item -ItemType Directory -Path $cacheDirectory -Force | Out-Null

if (Test-Path -LiteralPath $archivePath) {
    $existingHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA512).Hash.ToLowerInvariant()
    if ($existingHash -ne $expectedSha512) {
        Remove-Item -LiteralPath $archivePath -Force
    }
}

if (-not (Test-Path -LiteralPath $archivePath)) {
    Invoke-WebRequest -Uri $downloadUrl -OutFile $archivePath
}

$actualSha512 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA512).Hash.ToLowerInvariant()
if ($actualSha512 -ne $expectedSha512) {
    throw "SDK archive SHA-512 mismatch. Expected $expectedSha512 but received $actualSha512."
}

New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
Expand-Archive -LiteralPath $archivePath -DestinationPath $installDirectory -Force

if (-not (Test-Path -LiteralPath $dotnetPath)) {
    throw 'SDK extraction completed without dotnet.exe.'
}

$verifiedVersion = & $dotnetPath --version
if ($verifiedVersion -ne $sdkVersion) {
    throw "SDK verification failed. Expected $sdkVersion but found $verifiedVersion."
}

Write-Output "INSTALLED version=$verifiedVersion path=$dotnetPath sha512=$actualSha512"

