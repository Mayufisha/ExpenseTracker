#Requires -RunAsAdministrator

[CmdletBinding()]
param()

$ruleName = 'Money Manager local API block'
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($null -eq $existing) {
    New-NetFirewallRule `
        -DisplayName $ruleName `
        -Description 'Blocks remote inbound access to the loopback-only Money Manager development API.' `
        -Direction Inbound `
        -Action Block `
        -Protocol TCP `
        -LocalPort 5088 `
        -Profile Any | Out-Null
}

Get-NetFirewallRule -DisplayName $ruleName |
    Select-Object DisplayName, Enabled, Direction, Action
