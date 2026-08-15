[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$dotnet = Join-Path $projectRoot '.tools\dotnet-10.0.400\dotnet.exe'
$projects = @(
    (Join-Path $projectRoot 'src\Nara.HardwareProfiler\Nara.HardwareProfiler.csproj'),
    (Join-Path $projectRoot 'src\Nara.PolicyCompiler\Nara.PolicyCompiler.csproj'),
    (Join-Path $projectRoot 'src\Nara.ApprovalContracts\Nara.ApprovalContracts.csproj'),
    (Join-Path $projectRoot 'src\Nara.ApprovalBroker\Nara.ApprovalBroker.csproj'),
    (Join-Path $projectRoot 'src\Nara.PlaybookEngine\Nara.PlaybookEngine.csproj')
)
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

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw 'Local .NET SDK is missing. Run tools\bootstrap\Install-LocalDotNetSdk.ps1 first.'
}

foreach ($project in $projects) {
    & $dotnet restore $project --nologo --ignore-failed-sources --configfile $nugetConfig
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed: $project" }

    & $dotnet build $project --configuration $Configuration --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed: $project" }
}
