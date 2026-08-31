# Money Manager (.NET MAUI)

Money Manager is a cross-platform personal finance app built with .NET MAUI, SQLite, PostgreSQL, Supabase, and MVVM. It combines expense tracking, financial accounts, statement imports, goals, schedules, and shared-expense management. Builds with Supabase configuration use hosted authentication and per-user cross-device backups automatically; unconfigured development builds fall back to the loopback ASP.NET Core test server.

## Features

### Authentication and Sync

- Login or signup is required before financial data can be accessed.
- Users are never asked for backend URLs or API keys; production configuration is embedded at build time.
- Hosted builds use Supabase automatically. Unconfigured builds use the fixed loopback development server.
- Active session tokens are stored with the platform secure-storage service and validated or refreshed when the app starts again.
- Every SQLite row for transactions, goals, schedules, accounts, statements, and splits is scoped to the authenticated user ID.
- Users can sign out from Settings.
- Account data can be uploaded to or downloaded from the signed-in user's owner-scoped backup.

### Dashboard

- Tracks Income, Expenses, Assets, Liabilities, Net Cashflow, and Net Worth.
- Includes a 6-month net cashflow trend and financial composition charts.

### Transactions

- Add, edit, and delete transactions.
- Filter by month and financial institution.
- Imported transactions show their bank or credit-card source.
- Swipe an expense transaction from left to right to start a split.

### Shared Expenses and Fares

- Split any expense transaction, including restaurant bills, trip fares, rent, and imported card purchases.
- Divide equally between the current user and other participants, or enter custom participant shares.
- Store an optional email address or phone number for each participant.
- Track outstanding, collected, and settled amounts.
- Mark individual shares paid or unpaid.
- Create and share a Visa/Mastercard Checkout link in the Stripe test sandbox.
- Prepare Interac Request Money details and hand off to a participating bank website or app.
- Refresh webhook-verified card test payments into participant settlement status.
- Sync split and settlement state through the user's Supabase backup.

The Stripe adapter is test-only because Stripe prohibits personal peer-to-peer money transmission. Interac transfers are authorized and completed in the user's participating bank, outside Money Manager. The app never collects card numbers or bank-login credentials.

### Banks and Credit Cards

- Add and edit accounts from multiple financial institutions.
- Record institution name, account name/type, and optional last four digits.
- Store each account's current balance and balance date so net worth is calculated from balances, not statement-period cash flow.
- Attach CSV or PDF bank and credit-card statements.
- CSV and text-based PDF statements import transactions automatically.
- E-Transfers default to expenses; transfers marked `received`, `from`, `incoming`, or `autodeposit` are income.
- Existing imported transfers with these direction markers are corrected when transactions load.
- Statement sign conventions are inferred from known transfers, deposits, and direction columns; the account setting is only a fallback for ambiguous files.
- CSV files with separate debit and credit columns are classified automatically without using the amount-sign setting.
- Previously imported rows are reconciled against their attached statement when the dashboard or Transactions page first loads.
- Statements are limited to 10 MB, validated as CSV/PDF, and stored in the app's private data directory. Scanned-image PDFs require OCR or a CSV export.
- Duplicate statement files are detected using a SHA-256 file hash.
- When the hosted backend is enabled, failed statement uploads remain pending locally for retry.

### Goals and Schedule

- Add, edit, and delete savings goals with monthly deadline filtering.
- Add, edit, and delete scheduled income or payments with monthly filtering.
- Goals, schedules, and financial accounts use full inline editors instead of chained prompt dialogs.

### Settings and Appearance

- System, Light, and Dark theme preferences apply across all screens.
- Export or import a JSON backup.
- Manage sign-in and account backups.
- Backup imports are limited to 5 MB and validated before existing data is replaced.

## Statement CSV Format

The importer recognizes common column names used by financial institutions:

- Date: `Date`, `Transaction Date`, `Posted Date`, or `Posting Date`
- Description: `Description`, `Memo`, `Details`, `Name`, or `Transaction`
- Amount: `Amount` or `Transaction Amount`
- Separate amount columns: `Debit`/`Withdrawal`/`Charge` and `Credit`/`Deposit`/`Payment`

For a single `Amount` column, the importer first infers the sign convention from descriptions and direction values. If the file is ambiguous, bank accounts fall back to negative expenses and credit cards to positive expenses; the fallback can be changed in the inline account editor.

## Architecture

- `Models/` entities, backup DTOs, and enums
- `Services/` SQLite persistence, statement parsing/import, split allocation, payment-request sharing, backup, and account sync
- `ExpenseTracker.LocalServer/` local ASP.NET Core authentication, session, and backup API used during development
- `ViewModels/` page state and filtering logic
- `Views/` MAUI XAML pages and UI interaction code

## Tech Stack

- .NET 9 MAUI
- ASP.NET Core local development server
- SQLite (`sqlite-net-pcl`)
- PDF text extraction (`PdfPig`)
- Supabase Auth, PostgreSQL, Data REST API, Edge Functions, and private Storage
- Charts (`Microcharts.Maui`)

## Data Storage and Privacy

- Local database: `expenses.db3`
- User-owned database rows include an indexed `OwnerUserId`; records without an owner from pre-account builds are not shown to any newly authenticated user.
- Statement files: private application data under `Statements/<user-id>/`
- Development account backup: one JSON snapshot per authenticated user in the local server store
- Hosted cloud backup: one versioned JSONB snapshot per authenticated user in PostgreSQL
- Hosted statement storage: private `statements` bucket, scoped by Supabase user ID and SHA-256 object name
- Active development session token: platform `SecureStorage`
- Institution definitions and imported transaction data are included in backup/cloud sync.
- Local filesystem paths are never included in cloud backups.
- Account balances, split participants, shares, settlement state, and statement amount conventions are included in backup version 5.
- The publishable/anon key may be bundled in the client. Never place a Supabase `service_role` or secret key in this app.

## Local Development Server

The development server listens only on `http://127.0.0.1:5088`. Account records are written to `%LOCALAPPDATA%\MoneyManager\LocalServer\accounts.json`. New passwords use salted PBKDF2-HMAC-SHA256 with 600,000 iterations, older hashes upgrade after login, random session tokens are stored only as hashes by the server, and sessions expire after 7 days. The API also enforces loopback and Host validation, route-specific method/body/content-type rules, traversal and forwarding-header rejection, rate limits, and decoy-route monitoring. Security events are written as bounded JSON lines without request bodies, credentials, tokens, emails, query values, or raw user-agent values.

For defense in depth on Windows, run the idempotent inbound block rule from an elevated PowerShell terminal:

```powershell
.\scripts\Install-LocalFirewallRule.ps1
```

Do not expose this HTTP development server to a LAN or the internet. It is not the production multi-user backend.

Start the server first:

```powershell
dotnet run --project ExpenseTracker.LocalServer/ExpenseTracker.LocalServer.csproj
```

In a second terminal, start the Windows app:

```powershell
dotnet build ExpenseTracker.csproj -t:Run -f net9.0-windows10.0.19041.0
```

Create an account from the app. The saved session is restored on later launches while the local server is running. Use **Upload Backup** and **Download Backup** in Settings to test account-scoped backup behavior.

Set `MONEY_MANAGER_DATA_DIR` before starting the server to override its account-data directory for isolated testing.

## Hosted Supabase Setup

### 1. Create the project

1. Create a project at Supabase.
2. In Authentication, enable email/password sign-in.
3. Decide whether email confirmation is required. If enabled, configure the Site URL and email templates for the deployed app.

### 2. Apply the database migration

Apply the migrations in order through the Supabase CLI migration workflow or SQL Editor:

1. [`supabase/migrations/202608040001_initial_schema.sql`](supabase/migrations/202608040001_initial_schema.sql)
2. [`supabase/migrations/202608290001_payment_platform.sql`](supabase/migrations/202608290001_payment_platform.sql)
3. [`supabase/migrations/202608300001_security_hardening.sql`](supabase/migrations/202608300001_security_hardening.sql)

The migration creates:

- `public.user_backups`, keyed to `auth.users`
- Row Level Security policies that restrict every backup to its owner
- A private `statements` Storage bucket with a 10 MB per-file limit
- Storage policies that restrict statement paths to `<auth-user-id>/...`
- Owner-scoped payment accounts and payment requests
- An append-only provider-event table for webhook idempotency and reconciliation

### 3. Configure the hosted client

Use the project URL and publishable key from the Supabase dashboard. These values must be embedded at build or deployment time and must never be requested from an end user.

PowerShell:

```powershell
$env:SUPABASE_URL = "https://your-project.supabase.co"
$env:SUPABASE_PUBLISHABLE_KEY = "sb_publishable_..."
dotnet build ExpenseTracker.sln
```

MSBuild properties can also be supplied directly:

```powershell
dotnet build ExpenseTracker.sln `
  -p:SupabaseUrl="https://your-project.supabase.co" `
  -p:SupabasePublishableKey="sb_publishable_..."
```

The older JWT-style `anon` key is also supported. Do not use a secret key or the `service_role` key. When both hosted values are valid, `MauiProgram.cs` selects Supabase authentication, storage, backup, and the hosted payment sandbox automatically.

## Sync Behavior

- The app remains local-first; normal edits write to SQLite immediately.
- **Upload Backup** sends the current backup to the signed-in local-server or Supabase account selected at build time.
- **Download Backup** replaces the local transactions, accounts, statement metadata, goals, and schedule with that account's latest server backup.
- When the hosted backend is enabled, statements upload to private owner-scoped Storage and the backup persists in PostgreSQL under RLS.
- Split records use stable transaction GUIDs, so relationships survive a cross-device restore.
- Backup writes are last-write-wins. Upload from the device with the desired current data before downloading a server backup.
- Under the hosted backend, raw statement files remain in private Supabase Storage; backups restore metadata rather than device-local file copies.

## Security

No software is impossible to penetrate. Money Manager now includes defense-in-depth controls for account isolation, sessions, uploads, backups, the loopback API, Supabase RLS/Storage, and payment Functions, but production security also requires correct hosted configuration and ongoing operations.

Read [`docs/SECURITY.md`](docs/SECURITY.md) before deployment. Its release blockers include Supabase email confirmation, CAPTCHA, MFA support and enforcement, SSL/network controls, monitoring, signed Release builds, secret rotation, independent penetration testing, and a decision about application-level encryption for local financial data.

## Payment Setup

Payment infrastructure is optional. The app's expense tracking, statements, manual settlement, and Interac bank handoff work with the local server.

- Follow [`docs/PAYMENTS.md`](docs/PAYMENTS.md) to deploy the payment migration and Edge Functions.
- Stripe card checkout is restricted to test mode and does not move real money.
- Card checkout is intentionally unavailable while the local development gateway is active.
- Interac Request Money opens the user's configured online-banking URL after copying the request details.
- Production card or direct-transfer support requires an approved processor/network partner and a replacement production gateway adapter.

## Getting Started

### Prerequisites

- .NET 9 SDK
- MAUI workload
- Visual Studio 2022+ with MAUI support

### Build

```bash
dotnet restore
dotnet build ExpenseTracker.sln
```

### Test

```bash
dotnet test ExpenseTracker.Tests/ExpenseTracker.Tests.csproj
```

The local server must be running before signup, login, session restoration, or account backup will work. Supabase configuration is not required for local development.

## Product Roadmap

The next stages for Money Manager are tracked in [`docs/ROADMAP.md`](docs/ROADMAP.md). The payment roadmap deliberately separates payment requests from money movement so a future provider integration can meet security, reconciliation, identity, dispute, and app-store requirements.
