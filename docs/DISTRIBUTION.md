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
