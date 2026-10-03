[CmdletBinding()]
param(
    [ValidateSet('x64')]
    [string]$Architecture = 'x64',
    [string]$OutputDirectory = '',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'artifacts\local-test'
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
$serverOutput = Join-Path $staging 'LocalServer'
$packageOutput = Join-Path $output 'package'
New-Item -ItemType Directory -Path $serverOutput, $packageOutput -Force | Out-Null

if (-not $SkipTests) {
    & dotnet test (Join-Path $root 'ExpenseTracker.Tests\ExpenseTracker.Tests.csproj') `
        --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

& dotnet publish (Join-Path $root 'ExpenseTracker.LocalServer\ExpenseTracker.LocalServer.csproj') `
    --configuration Release `
    --runtime "win-$Architecture" `
    --self-contained true `
    --output $serverOutput
if ($LASTEXITCODE -ne 0) { throw 'Local server publish failed.' }

$publisher = 'CN=Money Manager Local Test'
$friendlyName = 'Money Manager local test package signing'
$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq $publisher `
            -and $_.FriendlyName -eq $friendlyName `
            -and $_.HasPrivateKey `
            -and $_.NotAfter -gt (Get-Date).AddDays(30)
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($null -eq $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $publisher `
        -KeyUsage DigitalSignature `
        -FriendlyName $friendlyName `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -NotAfter (Get-Date).AddYears(2) `
        -TextExtension @(
            '2.5.29.37={text}1.3.6.1.5.5.7.3.3',
            '2.5.29.19={text}'
        )
}

$certificatePath = Join-Path $packageOutput 'MoneyManager-LocalTest.cer'
Export-Certificate -Cert $certificate -FilePath $certificatePath -Force | Out-Null

$framework = 'net9.0-windows10.0.19041.0'
$packageDirectory = $packageOutput + [IO.Path]::DirectorySeparatorChar
& dotnet publish (Join-Path $root 'ExpenseTracker.csproj') `
    --configuration Release `
    --framework $framework `
    -p:TargetFrameworks=$framework `
    -p:WindowsPackageType=MSIX `
    -p:AppxPackageSigningEnabled=true `
    -p:PackageCertificateThumbprint=$($certificate.Thumbprint) `
    -p:BundledLocalServerPath=$serverOutput `
    -p:AppxPackageDir=$packageDirectory
if ($LASTEXITCODE -ne 0) { throw 'Money Manager MSIX publish failed.' }

$package = Get-ChildItem -LiteralPath $packageOutput -Recurse -File |
    Where-Object Extension -In '.msix', '.msixbundle' |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if ($null -eq $package) { throw 'The publish completed without producing an MSIX package.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
try {
    $serverEntry = $archive.Entries | Where-Object {
        $_.FullName.Replace('\', '/') -eq 'LocalServer/ExpenseTracker.LocalServer.exe'
    } | Select-Object -First 1
    if ($null -eq $serverEntry) { throw 'The MSIX does not contain the bundled local server.' }
}
finally {
    $archive.Dispose()
}

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-LocalTestPackage.ps1') `
    -Destination (Join-Path $packageOutput 'Install-MoneyManager.ps1')

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $package.FullName
$manifest = [ordered]@{
    package = [IO.Path]::GetRelativePath($packageOutput, $package.FullName).Replace('\', '/')
    sha256 = $hash.Hash
    certificate = (Split-Path $certificatePath -Leaf)
    certificateThumbprint = $certificate.Thumbprint
    publisher = $publisher
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    localTestOnly = $true
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageOutput 'release.json') -Encoding utf8

Write-Output "Package: $($package.FullName)"
Write-Output "Certificate: $certificatePath"
Write-Output "SHA256: $($hash.Hash)"
