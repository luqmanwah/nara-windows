[CmdletBinding()]
param([string]$Root=(Split-Path -Parent $PSScriptRoot))
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
[xml](Get-Content -LiteralPath (Join-Path $Root 'iso-overlay\autounattend.xml') -Raw) | Out-Null
Get-ChildItem -LiteralPath $Root -Recurse -Filter '*.json' | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json | Out-Null }
Get-ChildItem -LiteralPath $Root -Recurse -Filter '*.ps1' | ForEach-Object {
    $tokens=$null; $errors=$null
    [System.Management.Automation.Language.Parser]::ParseFile($_.FullName,[ref]$tokens,[ref]$errors) | Out-Null
    if($errors.Count){throw "PowerShell syntax error in $($_.FullName): $($errors[0].Message)"}
}
Write-Output 'PASS source validation'
