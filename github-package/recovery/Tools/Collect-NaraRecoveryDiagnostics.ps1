[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputRoot)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$destination = Join-Path $OutputRoot $stamp
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$installRoot = Join-Path $env:ProgramData 'Nara'
if (Test-Path -LiteralPath $installRoot) {
    Get-ChildItem -LiteralPath $installRoot -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in '.json','.jsonl','.log' } |
        ForEach-Object {
            $relative = $_.FullName.Substring($installRoot.Length).TrimStart('\')
            $target = Join-Path $destination $relative
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $target
        }
}
[ordered]@{ collectedAtUtc=[DateTime]::UtcNow.ToString('o'); windowsBuild=[Environment]::OSVersion.Version.Build; source=$installRoot } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'diagnostic-summary.json') -Encoding UTF8
Write-Host "Diagnostics copied to: $destination" -ForegroundColor Green
