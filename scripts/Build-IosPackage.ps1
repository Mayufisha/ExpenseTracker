[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$SupabaseUrl,
    [Parameter(Mandatory)] [string]$SupabasePublishableKey,
    [Parameter(Mandatory)] [string]$ApplicationId,
    [Parameter(Mandatory)] [string]$ApplicationDisplayVersion,
    [Parameter(Mandatory)] [long]$ApplicationVersion,
    [Parameter(Mandatory)] [string]$CodesignKey,
    [Parameter(Mandatory)] [string]$CodesignProvision,
    [string]$OutputDirectory = '',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $IsMacOS) { throw 'Signed iOS packages must be built on macOS.' }
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
& (Join-Path $PSScriptRoot 'Test-HostedRelease.ps1') `
    -SupabaseUrl $SupabaseUrl `
    -SupabasePublishableKey $SupabasePublishableKey `
    -ApplicationId $ApplicationId `
    -ApplicationDisplayVersion $ApplicationDisplayVersion `
    -ApplicationVersion $ApplicationVersion
if ([string]::IsNullOrWhiteSpace($CodesignKey) -or [string]::IsNullOrWhiteSpace($CodesignProvision)) {
    throw 'CodesignKey and CodesignProvision are required.'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'artifacts/ios'
} elseif (-not [IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $root $OutputDirectory
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
if (-not $output.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::Ordinal)) {
    throw 'OutputDirectory must be inside the repository artifacts directory.'
}
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }

$publishOutput = Join-Path $output 'staging/publish'
$verifyOutput = Join-Path $output 'staging/verify'
$packageOutput = Join-Path $output 'package'
New-Item -ItemType Directory -Path $publishOutput, $verifyOutput, $packageOutput -Force | Out-Null

if (-not $SkipTests) {
    & dotnet test (Join-Path $root 'ExpenseTracker.Tests/ExpenseTracker.Tests.csproj') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

$framework = 'net10.0-ios'
$publishArguments = @(
    'publish', (Join-Path $root 'ExpenseTracker.csproj'),
    '--configuration', 'Release',
    '--framework', $framework,
    '--runtime', 'ios-arm64',
    "-p:TargetFrameworks=$framework",
    '-p:ArchiveOnBuild=true',
    '-p:BuildIpa=true',
    "-p:ApplicationId=$ApplicationId",
    "-p:ApplicationDisplayVersion=$ApplicationDisplayVersion",
    "-p:ApplicationVersion=$ApplicationVersion",
    "-p:CodesignKey=$CodesignKey",
    "-p:CodesignProvision=$CodesignProvision",
    "-p:SupabaseUrl=$SupabaseUrl",
    "-p:SupabasePublishableKey=$SupabasePublishableKey",
    "-p:PublishDir=$publishOutput$([IO.Path]::DirectorySeparatorChar)"
)
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw 'Money Manager iOS publish failed.' }

$ipa = Get-ChildItem -LiteralPath $output -Recurse -File -Filter '*.ipa' |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if ($null -eq $ipa) { throw 'The publish completed without producing an IPA.' }

& ditto -x -k $ipa.FullName $verifyOutput
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the IPA.' }
$appBundle = Get-ChildItem -LiteralPath (Join-Path $verifyOutput 'Payload') -Directory -Filter '*.app' |
    Select-Object -First 1
if ($null -eq $appBundle) { throw 'The IPA does not contain a Payload app bundle.' }
if (Get-ChildItem -LiteralPath $appBundle.FullName -Recurse -Directory -Filter 'LocalServer' | Select-Object -First 1) {
    throw 'The iOS package contains the desktop local server.'
}
& codesign --verify --deep --strict $appBundle.FullName
if ($LASTEXITCODE -ne 0) { throw 'iOS code-signature verification failed.' }

$destinationName = "MoneyManager-$ApplicationDisplayVersion-$ApplicationVersion.ipa"
$destination = Join-Path $packageOutput $destinationName
Copy-Item -LiteralPath $ipa.FullName -Destination $destination
$release = [ordered]@{
    package = $destinationName
    sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash
    applicationId = $ApplicationId
    displayVersion = $ApplicationDisplayVersion
    buildNumber = $ApplicationVersion
    supabaseHost = ([Uri]$SupabaseUrl).Host
    backend = 'Supabase'
    localServerBundled = $false
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
}
$release | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageOutput 'release.json') -Encoding utf8

Write-Output "iOS package: $destination"
