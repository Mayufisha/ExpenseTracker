[CmdletBinding()]
param(
    [string]$PackagePath = '',
    [string]$CertificatePath = '',
    [switch]$TrustCertificateOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($CertificatePath)) {
    $CertificatePath = Join-Path $PSScriptRoot 'MoneyManager-LocalTest.cer'
}
if (-not (Test-Path -LiteralPath $CertificatePath)) { throw 'Money Manager signing certificate was not found.' }

$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($CertificatePath)
if ($certificate.Subject -ne 'CN=Money Manager Local Test') {
    throw 'The certificate publisher does not match the Money Manager local-test package.'
}
if ($certificate.NotAfter -le (Get-Date)) { throw 'The local-test signing certificate has expired.' }

if ($TrustCertificateOnly) {
    Import-Certificate -FilePath $CertificatePath -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
    return
}

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File |
        Where-Object Extension -In '.msix', '.msixbundle' |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not (Test-Path -LiteralPath $PackagePath)) { throw 'Money Manager MSIX package was not found.' }

$signature = Get-AuthenticodeSignature -LiteralPath $PackagePath
if ($null -eq $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
    throw 'The package signer does not match the supplied certificate.'
}
$isExpectedTrustFailure = $signature.Status -eq 'UnknownError' `
    -and $certificate.Subject -eq $certificate.Issuer
if ($signature.Status -ne 'Valid' -and -not $isExpectedTrustFailure) {
    throw "The package signature is not valid for the supplied certificate: $($signature.StatusMessage)"
}

$trusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint) -ErrorAction SilentlyContinue
if ($null -eq $trusted) {
    $escapedScriptPath = $PSCommandPath.Replace("'", "''")
    $escapedCertificatePath = $CertificatePath.Replace("'", "''")
    $command = "& '$escapedScriptPath' -CertificatePath '$escapedCertificatePath' -TrustCertificateOnly"
    $encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    $arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand $encodedCommand"
    $trustProcess = Start-Process powershell.exe -ArgumentList $arguments -Verb RunAs -WindowStyle Hidden -Wait -PassThru
    if ($trustProcess.ExitCode -ne 0) { throw 'The signing certificate was not trusted.' }
    $trusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint) -ErrorAction SilentlyContinue
    if ($null -eq $trusted) { throw 'The signing certificate was not added to the local machine trust store.' }
}

Add-AppxPackage -Path $PackagePath -ForceApplicationShutdown
Write-Output 'Money Manager local-test package installed successfully.'
