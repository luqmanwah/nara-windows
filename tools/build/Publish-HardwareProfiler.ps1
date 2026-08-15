[CmdletBinding()]
param(
    [ValidateSet('FrameworkDependent', 'SelfContained')]
    [string] $Mode = 'FrameworkDependent'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$dotnet = Join-Path $projectRoot '.tools\dotnet-10.0.400\dotnet.exe'
$project = Join-Path $projectRoot 'src\Nara.HardwareProfiler\Nara.HardwareProfiler.csproj'
$nugetConfig = Join-Path $projectRoot 'NuGet.Config'
$publishRelativePath = if ($Mode -eq 'SelfContained') {
    'artifacts\publish\hardware-profiler\win-x64-self-contained'
} else {
    'artifacts\publish\hardware-profiler\framework-dependent'
}
$publishDirectory = Join-Path $projectRoot $publishRelativePath

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
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

if ($Mode -eq 'SelfContained') {
    & $dotnet restore $project --runtime win-x64 --nologo --ignore-failed-sources --configfile $nugetConfig
    if ($LASTEXITCODE -ne 0) { throw 'RID restore failed. An official runtime pack source is required.' }

    & $dotnet publish $project `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --no-restore `
        --nologo `
        --output $publishDirectory `
        -p:PublishSingleFile=true `
        -p:PublishReadyToRun=false `
        -p:DebugType=None `
        -p:DebugSymbols=false
}
else {
    & $dotnet restore $project --nologo --ignore-failed-sources --configfile $nugetConfig
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

    & $dotnet publish $project `
        --configuration Release `
        --self-contained false `
        --no-restore `
        --nologo `
        --output $publishDirectory `
        -p:UseAppHost=false `
        -p:DebugType=None `
        -p:DebugSymbols=false
}

if ($LASTEXITCODE -ne 0) { throw "$Mode publish failed." }

$launchArtifact = if ($Mode -eq 'SelfContained') {
    Join-Path $publishDirectory 'Nara.HardwareProfiler.exe'
} else {
    Join-Path $publishDirectory 'Nara.HardwareProfiler.dll'
}
if (-not (Test-Path -LiteralPath $launchArtifact)) {
    throw 'Published launch artifact is missing.'
}

$size = (Get-Item -LiteralPath $launchArtifact).Length
$fileCount = @(Get-ChildItem -LiteralPath $publishDirectory -File).Count
Write-Output "PUBLISHED mode=$Mode path=$launchArtifact bytes=$size files=$fileCount"
