[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$SupabaseUrl,
    [Parameter(Mandatory)] [string]$SupabasePublishableKey,
    [Parameter(Mandatory)] [string]$CertificateThumbprint,
    [Parameter(Mandatory)] [string]$Publisher,
    [Parameter(Mandatory)] [string]$ApplicationId,
    [string]$ApplicationDisplayVersion = '1.0.0',
    [long]$ApplicationVersion = 1
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

& (Join-Path $PSScriptRoot 'Test-HostedRelease.ps1') `
    -SupabaseUrl $SupabaseUrl `
    -SupabasePublishableKey $SupabasePublishableKey `
    -ApplicationId $ApplicationId `
    -ApplicationDisplayVersion $ApplicationDisplayVersion `
    -ApplicationVersion $ApplicationVersion

if ($Publisher -match '(?i)(Local Test|User Name|Example|Placeholder)') {
    throw 'Production Publisher must not contain a placeholder or local-test identity.'
}
if ($ApplicationId.Length -gt 50) {
    throw 'The Windows ApplicationId must be at most 50 characters.'
}
if ($CertificateThumbprint -notmatch '^[A-Fa-f0-9]{40,64}$') {
    throw 'CertificateThumbprint must contain only 40-64 hexadecimal characters.'
}

$certificate = Get-Item "Cert:\CurrentUser\My\$CertificateThumbprint" -ErrorAction Stop
if (-not $certificate.HasPrivateKey) { throw 'The production signing certificate has no private key.' }
if ($certificate.NotAfter -le (Get-Date).AddDays(30)) { throw 'The production signing certificate expires within 30 days.' }
if ($certificate.Subject -ne $Publisher) { throw 'The signing certificate subject does not match Publisher.' }
$codeSigningOid = '1.3.6.1.5.5.7.3.3'
$enhancedKeyUsageOids = @($certificate.EnhancedKeyUsageList | ForEach-Object ObjectId)
if ($codeSigningOid -notin $enhancedKeyUsageOids) {
    throw 'The certificate is not valid for code signing.'
}

Write-Output 'Production release inputs passed local validation.'
