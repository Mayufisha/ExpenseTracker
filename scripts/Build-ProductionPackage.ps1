[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$SupabaseUrl,
    [Parameter(Mandatory)] [string]$SupabasePublishableKey,
    [Parameter(Mandatory)] [string]$CertificateThumbprint,
    [Parameter(Mandatory)] [string]$Publisher,
    [Parameter(Mandatory)] [string]$PublisherDisplayName,
    [Parameter(Mandatory)] [string]$ApplicationId,
    [ValidateSet('x64')] [string]$Architecture = 'x64',
    [string]$OutputDirectory = '',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
& (Join-Path $PSScriptRoot 'Test-ProductionRelease.ps1') `
    -SupabaseUrl $SupabaseUrl `
    -SupabasePublishableKey $SupabasePublishableKey `
    -CertificateThumbprint $CertificateThumbprint `
    -Publisher $Publisher `
    -ApplicationId $ApplicationId

if ([string]::IsNullOrWhiteSpace($PublisherDisplayName)) {
    throw 'PublisherDisplayName is required.'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'artifacts\production'
}
elseif (-not [IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $root $OutputDirectory
}

$output = [IO.Path]::GetFullPath($OutputDirectory)
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
if (-not $output.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be inside the repository artifacts directory.'
}
if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

$staging = Join-Path $output 'staging'
$packageOutput = Join-Path $output 'package'
New-Item -ItemType Directory -Path $staging, $packageOutput -Force | Out-Null

$manifestPath = Join-Path $staging 'Package.appxmanifest'
[xml]$manifest = Get-Content -LiteralPath (Join-Path $root 'Platforms\Windows\Package.appxmanifest') -Raw
$manifest.Package.Identity.Publisher = $Publisher
$manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName
$xmlSettings = [Xml.XmlWriterSettings]::new()
$xmlSettings.Indent = $true
$xmlSettings.Encoding = [Text.UTF8Encoding]::new($false)
$writer = [Xml.XmlWriter]::Create($manifestPath, $xmlSettings)
try { $manifest.Save($writer) } finally { $writer.Dispose() }

if (-not $SkipTests) {
    & dotnet test (Join-Path $root 'ExpenseTracker.Tests\ExpenseTracker.Tests.csproj') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

$framework = 'net10.0-windows10.0.19041.0'
$packageDirectory = $packageOutput + [IO.Path]::DirectorySeparatorChar
$publishArguments = @(
    'publish',
    (Join-Path $root 'ExpenseTracker.csproj'),
    '--configuration', 'Release',
    '--framework', $framework,
    "-p:TargetFrameworks=$framework",
    '-p:WindowsPackageType=MSIX',
    '-p:AppxPackageSigningEnabled=true',
    "-p:PackageCertificateThumbprint=$CertificateThumbprint",
    "-p:PackageManifest=$manifestPath",
    "-p:ApplicationId=$ApplicationId",
    "-p:SupabaseUrl=$SupabaseUrl",
    "-p:SupabasePublishableKey=$SupabasePublishableKey",
    "-p:AppxPackageDir=$packageDirectory"
)
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw 'Money Manager production MSIX publish failed.' }

$package = Get-ChildItem -LiteralPath $packageOutput -Recurse -File |
    Where-Object Extension -In '.msix', '.msixbundle' |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if ($null -eq $package) { throw 'The publish completed without producing an MSIX package.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
try {
    $bundledServer = $archive.Entries | Where-Object {
        $_.FullName.Replace('\', '/').StartsWith('LocalServer/', [StringComparison]::OrdinalIgnoreCase)
    } | Select-Object -First 1
    if ($null -ne $bundledServer) { throw 'A production package must not contain the local development server.' }
}
finally {
    $archive.Dispose()
}

$signature = Get-AuthenticodeSignature -LiteralPath $package.FullName
if ($signature.Status -ne 'Valid' `
    -or $null -eq $signature.SignerCertificate `
    -or $signature.SignerCertificate.Thumbprint -ne $CertificateThumbprint) {
    throw "The production package signature is invalid: $($signature.StatusMessage)"
}

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $package.FullName
$release = [ordered]@{
    package = [IO.Path]::GetRelativePath($packageOutput, $package.FullName).Replace('\', '/')
    sha256 = $hash.Hash
    certificateThumbprint = $CertificateThumbprint
    publisher = $Publisher
    applicationId = $ApplicationId
    supabaseHost = ([Uri]$SupabaseUrl).Host
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    localTestOnly = $false
}
$release | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageOutput 'release.json') -Encoding utf8

Write-Output "Package: $($package.FullName)"
Write-Output "SHA256: $($hash.Hash)"
