Set-StrictMode -Version Latest

function Get-NaraRegistrySnapshot {
    param([Parameter(Mandatory)] [array] $Settings)
    @($Settings | ForEach-Object {
        $exists = $false; $value = $null; $kind = $null
        if (Test-Path -LiteralPath $_.Path) {
            try {
                $key = Get-Item -LiteralPath $_.Path -ErrorAction Stop
                $value = $key.GetValue($_.Name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                if ($null -ne $value) { $exists = $true; $kind = [string] $key.GetValueKind($_.Name) }
            } catch {}
        }
        [ordered]@{ path = $_.Path; name = $_.Name; existed = $exists; kind = $kind; value = $value }
    })
}

function Set-NaraRegistryValue {
    param([Parameter(Mandatory)] $Setting)
    if (-not (Test-Path -LiteralPath $Setting.Path)) { New-Item -Path $Setting.Path -Force | Out-Null }
    New-ItemProperty -LiteralPath $Setting.Path -Name $Setting.Name -Value $Setting.Value -PropertyType $Setting.Kind -Force | Out-Null
}

function Restore-NaraRegistrySnapshot {
    param([Parameter(Mandatory)] [array] $Snapshot)
    foreach ($item in $Snapshot) {
        if ($item.existed) {
            if (-not (Test-Path -LiteralPath $item.path)) { New-Item -Path $item.path -Force | Out-Null }
            New-ItemProperty -LiteralPath $item.path -Name $item.name -Value $item.value -PropertyType $item.kind -Force | Out-Null
        } elseif (Test-Path -LiteralPath $item.path) {
            Remove-ItemProperty -LiteralPath $item.path -Name $item.name -ErrorAction SilentlyContinue
        }
    }
}
