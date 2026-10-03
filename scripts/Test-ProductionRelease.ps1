[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$SupabaseUrl,
    [Parameter(Mandatory)] [string]$SupabasePublishableKey,
    [Parameter(Mandatory)] [string]$CertificateThumbprint,
    [Parameter(Mandatory)] [string]$Publisher,
    [Parameter(Mandatory)] [string]$ApplicationId
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$uri = $null
if (-not [Uri]::TryCreate($SupabaseUrl, [UriKind]::Absolute, [ref]$uri) `
    -or $uri.Scheme -ne 'https' `
    -or -not [string]::IsNullOrEmpty($uri.UserInfo) `
    -or $uri.AbsolutePath -ne '/' `
    -or -not [string]::IsNullOrEmpty($uri.Query) `
    -or -not [string]::IsNullOrEmpty($uri.Fragment)) {
    throw 'SupabaseUrl must be a credential-free HTTPS origin without a path, query, or fragment.'
}
if ([string]::IsNullOrWhiteSpace($SupabasePublishableKey) `
    -or $SupabasePublishableKey.Length -gt 4096 `
    -or $SupabasePublishableKey -notmatch '^[A-Za-z0-9._-]+$' `
    -or $SupabasePublishableKey -match '(service_role|secret)') {
    throw 'Provide a Supabase publishable or legacy anon key, never a secret/service-role key.'
}
if ($Publisher -match '(Local Test|User Name|Example|Placeholder)' `
    -or $ApplicationId -match '(companyname|localtest|example|placeholder)') {
    throw 'Production Publisher and ApplicationId values must not contain placeholder or local-test identities.'
}
if ($ApplicationId.Length -gt 50 `
    -or $ApplicationId -notmatch '^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z0-9][A-Za-z0-9-]*)+$') {
    throw 'ApplicationId must be a reverse-domain identifier of at most 50 characters.'
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
