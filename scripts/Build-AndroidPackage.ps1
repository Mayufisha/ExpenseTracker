[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$SupabaseUrl,
    [Parameter(Mandatory)] [string]$SupabasePublishableKey,
    [Parameter(Mandatory)] [string]$ApplicationId,
    [Parameter(Mandatory)] [string]$ApplicationDisplayVersion,
    [Parameter(Mandatory)] [long]$ApplicationVersion,
    [Parameter(Mandatory)] [string]$KeystorePath,
    [Parameter(Mandatory)] [string]$KeyAlias,
    [Parameter(Mandatory)] [string]$StorePasswordFile,
    [string]$KeyPasswordFile = '',
    [string]$OutputDirectory = '',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
& (Join-Path $PSScriptRoot 'Test-HostedRelease.ps1') `
    -SupabaseUrl $SupabaseUrl `
    -SupabasePublishableKey $SupabasePublishableKey `
    -ApplicationId $ApplicationId `
    -ApplicationDisplayVersion $ApplicationDisplayVersion `
    -ApplicationVersion $ApplicationVersion

if ($KeyAlias -notmatch '^[A-Za-z0-9._-]{1,100}$') {
    throw 'KeyAlias must contain only letters, numbers, periods, underscores, or hyphens.'
}
$keystore = (Resolve-Path -LiteralPath $KeystorePath).Path
$storePassword = (Resolve-Path -LiteralPath $StorePasswordFile).Path
$keyPassword = if ([string]::IsNullOrWhiteSpace($KeyPasswordFile)) {
    $storePassword
} else {
    (Resolve-Path -LiteralPath $KeyPasswordFile).Path
}
foreach ($path in @($keystore, $storePassword, $keyPassword)) {
    if ((Get-Item -LiteralPath $path).Length -eq 0) { throw "Signing input is empty: $path" }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'artifacts/android'
} elseif (-not [IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $root $OutputDirectory
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
if (-not $output.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be inside the repository artifacts directory.'
}
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }

$publishOutput = Join-Path $output 'staging/publish'
$packageOutput = Join-Path $output 'package'
New-Item -ItemType Directory -Path $publishOutput, $packageOutput -Force | Out-Null
$defaultPackageOutput = Join-Path $root 'bin/Release/net10.0-android'
if (Test-Path -LiteralPath $defaultPackageOutput) {
    Get-ChildItem -LiteralPath $defaultPackageOutput -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -in @("$ApplicationId-Signed.aab", "$ApplicationId-Signed.apk") } |
        Remove-Item -Force
}

if (-not $SkipTests) {
    & dotnet test (Join-Path $root 'ExpenseTracker.Tests/ExpenseTracker.Tests.csproj') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

$framework = 'net10.0-android'
$publishArguments = @(
    'publish', (Join-Path $root 'ExpenseTracker.csproj'),
    '--configuration', 'Release',
    '--framework', $framework,
    "-p:TargetFrameworks=$framework",
    "-p:ApplicationId=$ApplicationId",
    "-p:ApplicationDisplayVersion=$ApplicationDisplayVersion",
    "-p:ApplicationVersion=$ApplicationVersion",
    "-p:SupabaseUrl=$SupabaseUrl",
    "-p:SupabasePublishableKey=$SupabasePublishableKey",
    '-p:AndroidKeyStore=true',
    "-p:AndroidSigningKeyStore=$keystore",
    "-p:AndroidSigningKeyAlias=$KeyAlias",
    "-p:AndroidSigningStorePass=file:$storePassword",
    "-p:AndroidSigningKeyPass=file:$keyPassword",
    '-p:AndroidPackageFormats=aab%3Bapk',
    "-p:PublishDir=$publishOutput$([IO.Path]::DirectorySeparatorChar)"
)
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw 'Money Manager Android publish failed.' }

$searchRoots = @($publishOutput, $defaultPackageOutput)
$packages = @()
foreach ($extension in @('.aab', '.apk')) {
    $package = Get-ChildItem -LiteralPath $searchRoots -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -eq "$ApplicationId-Signed$extension"
        } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $package) {
        throw "The publish completed without producing a signed $extension package."
    }
    $packages += $package
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($package in $packages) {
    $archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $bundledServer = $archive.Entries | Where-Object {
            $_.FullName.Replace('\', '/') -match '(^|/)LocalServer/'
        } | Select-Object -First 1
        if ($null -ne $bundledServer) { throw "Mobile package contains the desktop local server: $($package.Name)" }
    } finally {
        $archive.Dispose()
    }
}

$javaHomeJarsigner = if ([string]::IsNullOrWhiteSpace($env:JAVA_HOME)) {
    $null
} else {
    Join-Path $env:JAVA_HOME 'bin/jarsigner.exe'
}
$jarsignerCandidates = @(
    (Get-Command jarsigner -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source),
    $javaHomeJarsigner,
    'C:\Program Files\Android\Android Studio\jbr\bin\jarsigner.exe'
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) }
$jarsigner = $jarsignerCandidates | Select-Object -First 1
if ($null -eq $jarsigner) { throw 'jarsigner was not found on PATH or in the Android Studio JBR.' }
$bundle = $packages | Where-Object Extension -EQ '.aab' | Select-Object -First 1
$aabVerification = (& $jarsigner -verify $bundle.FullName 2>&1 | Out-String)
$aabVerification | Write-Output
if ($LASTEXITCODE -ne 0 `
    -or $aabVerification -notmatch '(?i)jar verified' `
    -or $aabVerification -match '(?i)jar is unsigned') {
    throw 'AAB signature verification failed.'
}

$sdkRoots = @($env:ANDROID_SDK_ROOT, $env:ANDROID_HOME, 'C:\Android\android-sdk') |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) }
$apksigner = $null
foreach ($sdkRoot in $sdkRoots) {
    $apksigner = Get-ChildItem -LiteralPath (Join-Path $sdkRoot 'build-tools') -Directory -ErrorAction SilentlyContinue |
        Sort-Object { try { [version]$_.Name } catch { [version]'0.0' } } -Descending |
        ForEach-Object { Get-Item -LiteralPath (Join-Path $_.FullName 'apksigner.bat') -ErrorAction SilentlyContinue } |
        Select-Object -First 1
    if ($null -ne $apksigner) { break }
}
if ($null -eq $apksigner) { throw 'Android SDK apksigner was not found.' }
$apk = $packages | Where-Object Extension -EQ '.apk' | Select-Object -First 1
& $apksigner.FullName verify --verbose --print-certs $apk.FullName | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'APK signature verification failed.' }

$releasePackages = @()
foreach ($package in $packages) {
    $destinationName = "MoneyManager-$ApplicationDisplayVersion-$ApplicationVersion-Signed$($package.Extension)"
    $destination = Join-Path $packageOutput $destinationName
    Copy-Item -LiteralPath $package.FullName -Destination $destination
    $releasePackages += [ordered]@{
        file = $destinationName
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash
    }
}
$release = [ordered]@{
    packages = $releasePackages
    applicationId = $ApplicationId
    displayVersion = $ApplicationDisplayVersion
    buildNumber = $ApplicationVersion
    supabaseHost = ([Uri]$SupabaseUrl).Host
    backend = 'Supabase'
    localServerBundled = $false
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
}
$release | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $packageOutput 'release.json') -Encoding utf8

Write-Output "Android packages: $packageOutput"
