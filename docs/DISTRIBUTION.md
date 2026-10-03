# Money Manager Distribution

## Windows Local-Test Package

The local-test package is intended for personal testing and trusted testers. It contains both the Money Manager desktop app and a self-contained loopback server, so testers do not need the repository, .NET SDK, or a separate server terminal.

Build it from the repository root:

```powershell
.\scripts\Build-LocalTestPackage.ps1
```

The command runs the test suite, publishes the server, creates or reuses a local self-signed code-signing certificate, signs an x64 MSIX, verifies that the server is inside the package, and writes a SHA-256 release manifest under `artifacts/local-test/package/`.

Distribute the complete `package` directory. On a tester's Windows computer, open PowerShell in that directory and run:

```powershell
.\Install-MoneyManager.ps1
```

Windows requests administrator consent once to add the local-test public certificate to the machine's Trusted People store. The private signing key is never exported. The installer pins the package signer to the included certificate before requesting trust, and Windows validates the package again during installation.

After installation, launch **Money Manager** from the Start menu. The bundled server starts hidden on `127.0.0.1:5088` and stops when the app closes normally. It remains a local-only test backend; accounts and backups do not sync between computers.

The same package can be produced from the manual **Windows Local-Test Package** GitHub Actions workflow. Its downloadable artifact is retained for 14 days.

## Production Package

A production package uses Supabase and must not contain or launch the loopback server. Obtain a trusted Windows code-signing certificate, import its private key into `Cert:\CurrentUser\My`, configure a dedicated production Supabase project, and run:

```powershell
.\scripts\Build-ProductionPackage.ps1 `
  -SupabaseUrl "https://your-project.supabase.co" `
  -SupabasePublishableKey "sb_publishable_..." `
  -CertificateThumbprint "CERTIFICATE_THUMBPRINT" `
  -Publisher "CERTIFICATE_SUBJECT" `
  -PublisherDisplayName "Your Company" `
  -ApplicationId "com.yourcompany.moneymanager"
```

The production command fails if it detects placeholder identities, a non-HTTPS URL, a secret/service-role key, an expiring or mismatched certificate, an invalid signature, or a bundled local server. Successful output is written under `artifacts/production/package/`.

Generating a production MSIX is not the same as approving a public release. Complete every blocker in `docs/SECURITY.md`, validate the deployed Supabase policies and storage rules with multiple accounts, complete independent penetration testing, establish incident-response and backup procedures, and use Microsoft Store or another controlled signed distribution channel.

## Windows Packaging Notes

- A self-signed certificate is suitable only for controlled testing because every tester must explicitly trust it.
- Public distribution should use a certificate whose chain is already trusted by Windows or Microsoft Store signing.
- Never commit a PFX file, certificate password, Supabase secret key, or `service_role` key.
- Keep the local-test and production package identities separate to avoid accidental upgrades across trust boundaries.

## Mobile Release Identity

Choose one permanent reverse-domain identifier, such as `com.yourcompany.moneymanager`, and register that exact value with Google Play Console and Apple Developer before the first public release. The release scripts reject the source placeholder and require the identity explicitly; they do not silently publish `com.companyname.expensetracker`.

Use a two- or three-part display version such as `1.0.0`. Use an integer build number that increases for every store upload. The workflows pass these values to Android `versionName`/`versionCode` and iOS `CFBundleShortVersionString`/`CFBundleVersion` through MAUI's application version properties.

Every mobile release is configured for Supabase at build time. If the URL and publishable key are absent, the app displays a hosted-backend configuration error. It never attempts to start the Windows executable or connect to the loopback development server.

## Android Signed APK And AAB

Create and protect a production Android upload keystore. Do not commit the keystore or its passwords. Google Play App Signing should hold the app-signing key while the CI keystore remains the replaceable upload key.

Run a signed build locally from PowerShell:

```powershell
.\scripts\Build-AndroidPackage.ps1 `
  -SupabaseUrl "https://your-project.supabase.co" `
  -SupabasePublishableKey "sb_publishable_..." `
  -ApplicationId "com.yourcompany.moneymanager" `
  -ApplicationDisplayVersion "1.0.0" `
  -ApplicationVersion 1 `
  -KeystorePath "C:\secure\money-manager.keystore" `
  -KeyAlias "money-manager-upload" `
  -StorePasswordFile "C:\secure\store-password.txt" `
  -KeyPasswordFile "C:\secure\key-password.txt"
```

The command runs tests, publishes signed APK and AAB files, rejects any bundled desktop server, verifies the AAB with `jarsigner`, verifies the APK with Android SDK `apksigner`, and writes SHA-256 hashes to `artifacts/android/package/release.json`. The Android package stack uses 16 KB-page-compatible native libraries for current Google Play requirements.

For CI, create a protected GitHub environment named `mobile-production`, restrict its reviewers and deployment branches, and add these environment secrets:

- `SUPABASE_URL`
- `SUPABASE_PUBLISHABLE_KEY`
- `ANDROID_KEYSTORE_BASE64`
- `ANDROID_KEYSTORE_PASSWORD`
- `ANDROID_KEY_PASSWORD` (optional when it matches the store password)
- `ANDROID_KEY_ALIAS`

Run **Android Signed Release**, enter the registered application ID, display version, and increasing build number, then download the retained artifact. Upload the AAB to the intended Google Play track. The workflow deliberately does not upload to Google Play automatically, so promotion remains a separate reviewed action.

## iOS Signed IPA And TestFlight

iOS signing requires macOS, an Apple Distribution certificate with its private key, and an App Store provisioning profile for the exact bundle identifier. Run locally on a configured Mac:

```powershell
./scripts/Build-IosPackage.ps1 `
  -SupabaseUrl "https://your-project.supabase.co" `
  -SupabasePublishableKey "sb_publishable_..." `
  -ApplicationId "com.yourcompany.moneymanager" `
  -ApplicationDisplayVersion "1.0.0" `
  -ApplicationVersion 1 `
  -CodesignKey "Apple Distribution: Your Company (TEAMID)" `
  -CodesignProvision "PROFILE_UUID"
```

The command runs tests, creates the device IPA, rejects any bundled desktop server, verifies the extracted app with `codesign`, and writes a SHA-256 release manifest under `artifacts/ios/package/`.

Add these protected `mobile-production` environment secrets for the **iOS Signed Release** workflow:

- `SUPABASE_URL`
- `SUPABASE_PUBLISHABLE_KEY`
- `IOS_CERTIFICATE_P12_BASE64`
- `IOS_CERTIFICATE_PASSWORD`
- `IOS_CODESIGN_KEY`
- `IOS_PROVISIONING_PROFILE_BASE64`

Optional TestFlight upload also requires:

- `APP_STORE_CONNECT_API_KEY_ID`
- `APP_STORE_CONNECT_ISSUER_ID`
- `APP_STORE_CONNECT_PRIVATE_KEY_BASE64`

The workflow uses the current `macos-26` GitHub runner, imports signing material into a temporary keychain, installs the provisioning profile, builds and verifies the IPA, and removes temporary credentials even when a step fails. Select **Upload to TestFlight** only after the App Store Connect app record exists for the supplied bundle ID. Apple still processes and validates the uploaded build before it becomes available to testers.

## Mobile Store Checklist

- Keep Android upload keys, Apple certificates, provisioning profiles, and App Store Connect API keys in the protected environment only.
- Enable required reviewers for the `mobile-production` environment and protect the release branch.
- Complete the App Store privacy questionnaire for financial data, contact data, identifiers, diagnostics, and any future payment collection. The included privacy manifest declares the required system APIs used by MAUI and app preferences; it does not replace App Store Connect disclosures.
- Prepare store listings, screenshots, support and privacy-policy URLs, age ratings, tester instructions, and account-deletion/support processes.
- Test release binaries on physical Android and iOS devices, including fresh install, session restoration, statement upload, account isolation, offline failure, and upgrade behavior.
- Complete the security blockers in `docs/SECURITY.md` before inviting external users or uploading real financial statements.
