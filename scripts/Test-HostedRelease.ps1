[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$SupabaseUrl,
    [Parameter(Mandatory)] [string]$SupabasePublishableKey,
    [Parameter(Mandatory)] [string]$ApplicationId,
    [Parameter(Mandatory)] [string]$ApplicationDisplayVersion,
    [Parameter(Mandatory)] [long]$ApplicationVersion
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
    -or $SupabasePublishableKey -match '(?i)(service_role|secret)') {
    throw 'Provide a Supabase publishable or legacy anon key, never a secret/service-role key.'
}
if ($ApplicationId -match '(?i)(companyname|localtest|example|placeholder)' `
    -or $ApplicationId.Length -gt 100 `
    -or $ApplicationId -notmatch '^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z0-9][A-Za-z0-9-]*)+$') {
    throw 'ApplicationId must be a non-placeholder reverse-domain identifier of at most 100 characters.'
}
if ($ApplicationDisplayVersion -notmatch '^\d+\.\d+(\.\d+)?$') {
    throw 'ApplicationDisplayVersion must contain two or three numeric components, such as 1.0.0.'
}
if ($ApplicationVersion -lt 1 -or $ApplicationVersion -gt 2100000000) {
    throw 'ApplicationVersion must be an integer from 1 through 2100000000.'
}

Write-Output 'Hosted release inputs passed validation.'
