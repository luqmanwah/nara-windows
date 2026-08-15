[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Destination,
    [Parameter(Mandatory)] [string] $LogPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$parent = Split-Path -Parent ([IO.Path]::GetFullPath($LogPath))
if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
New-Item -ItemType Directory -Path $Destination -Force | Out-Null

$driver = Get-CimInstance Win32_PnPSignedDriver | Where-Object {
    $_.DeviceName -match 'RTL8723BE' -or $_.DeviceID -match 'VEN_10EC&DEV_B723'
} | Select-Object -First 1
if ($null -eq $driver -or [string]::IsNullOrWhiteSpace([string] $driver.InfName)) {
    throw 'Driver RTL8723BE yang aktif tidak ditemukan.'
}

$lines = @(
    "Device: $($driver.DeviceName)"
    "INF: $($driver.InfName)"
    "Version: $($driver.DriverVersion)"
    "Provider: $($driver.DriverProviderName)"
    "Signed: $($driver.IsSigned)"
)
$lines | Set-Content -LiteralPath $LogPath -Encoding UTF8
& pnputil.exe /export-driver ([string] $driver.InfName) $Destination 2>&1 | Tee-Object -FilePath $LogPath -Append
if ($LASTEXITCODE -ne 0) { throw "pnputil export gagal dengan kode $LASTEXITCODE" }
if (-not (Get-ChildItem -LiteralPath $Destination -Recurse -Filter '*.inf' -ErrorAction SilentlyContinue)) {
    throw 'Ekspor selesai tanpa menemukan file INF.'
}
