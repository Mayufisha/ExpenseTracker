# Money Manager 1.0 release preparation

## Product decision

SplitFare is the shared-expenses section inside Money Manager. It uses the same signed-in account, transactions, SQLite database, and hosted Supabase backup. The separate Kotlin/MongoDB SplitFare scaffold is retained for reference; it is not part of this release and has not been deleted or archived.

## First-release scope

- Android and Windows.
- Existing accounts, transactions, bank-statement imports, goals, schedules, dashboard, and backup features.
- SplitFare: equal/custom expense shares, participant contacts, balances, manual settlement, and external e-Transfer bank handoff.
- Atomic split persistence, service-level duplicate protection, positive cent-based share validation, and account/parent checks on settlement updates.
- Retryable SplitFare loading, live allocation preview, and recoverable settlement errors.
- Card-test actions hidden in Release builds. Shared invitations, realtime group collaboration, and production card processing are outside this release.

## Publishing prerequisites

These are required inputs, not completed publication steps:

1. Sign into Supabase and create/select the dedicated production project. Apply the tracked migrations, validate owner isolation with two accounts, and complete the production checks in `docs/SECURITY.md`.
2. Configure the production URL and **publishable** key using the release scripts or GitHub environment. Never supply a service-role key to the app.
3. Verify Google Play Console enrollment. Register the final package ID, configure the upload keystore/Play App Signing, prepare the store listing and data-safety declarations, and begin with an internal testing track.
4. Choose Microsoft Store distribution or obtain a trusted Windows code-signing certificate. The existing local-test certificate is not public-release signing material.
5. Verify the final permanent application ID and increasing build numbers before the first store upload. `dev.samuelmolla.moneymanager` is a possible identifier based on the owned domain, not a registered or approved store identity.
6. Test the signed release builds on an Android device and Windows, including account creation/sign-in/out, account switching, imports, backup restore, and SplitFare. Validate release behavior against the actual production project before approving public access.

## Existing release tooling

- `scripts/Build-AndroidPackage.ps1`: signed APK/AAB, hosted backend required.
- `.github/workflows/android-release.yml`: Android signed release workflow; requires the `mobile-production` environment and its signing/backend configuration.
- `scripts/Build-ProductionPackage.ps1`: trusted signed Windows MSIX with hosted backend and no loopback server.
- `docs/DISTRIBUTION.md`: setup and packaging commands for each platform.
- `.github/workflows/dotnet-maui.yml`: tests plus separate Android/Windows compilation jobs, avoiding unsupported iOS/macOS builds on Windows runners.

Do not label unconfigured builds or self-signed local-test packages as the public production release. No public store release is claimed by this document.
