# Money Manager (.NET MAUI)

Money Manager is a cross-platform personal finance app built with .NET MAUI, SQLite, PostgreSQL, Supabase, and MVVM. It combines expense tracking, financial accounts, statement imports, goals, schedules, and shared-expense management. SQLite provides the offline device cache; Supabase provides accounts, cross-device backups, and private statement storage.

## Features

### Authentication and Sync

- Login or signup is required before financial data can be accessed.
- The active Supabase session is stored in the platform secure-storage service and refreshed when the app starts again.
- Users can sign out from Settings.
- Account data can be uploaded to or downloaded from the user's PostgreSQL-backed cloud snapshot.

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
- Attach CSV or PDF bank and credit-card statements.
- CSV statements import transactions automatically.
- Statements are stored in the app's private data directory and uploaded to the user's private Supabase Storage path.
- Duplicate statement files are detected using a SHA-256 file hash.
- Failed statement uploads remain pending locally and retry during the next upload sync.

### Goals and Schedule

- Add, edit, and delete savings goals with monthly deadline filtering.
- Add and delete scheduled payments with monthly filtering.

### Settings and Appearance

- System, Light, and Dark theme preferences apply across all screens.
- Export or import a JSON backup.
- Manage sign-in and cloud synchronization.

## Statement CSV Format

The importer recognizes common column names used by financial institutions:

- Date: `Date`, `Transaction Date`, `Posted Date`, or `Posting Date`
- Description: `Description`, `Memo`, `Details`, `Name`, or `Transaction`
- Amount: `Amount` or `Transaction Amount`
- Separate amount columns: `Debit`/`Withdrawal`/`Charge` and `Credit`/`Deposit`/`Payment`

For bank accounts, negative amounts are expenses and positive amounts are income. For credit cards, positive amounts are treated as charges and negative amounts as credits/refunds.

## Architecture

- `Models/` entities, backup DTOs, and enums
- `Services/` SQLite persistence, statement parsing/import, split allocation, payment-request sharing, backup, and account sync
- `ViewModels/` page state and filtering logic
- `Views/` MAUI XAML pages and UI interaction code

## Tech Stack

- .NET 9 MAUI
- SQLite (`sqlite-net-pcl`)
- Supabase Auth, PostgreSQL, Data REST API, and Storage
- Charts (`Microcharts.Maui`)

## Data Storage and Privacy

- Local database: `expenses.db3`
- Statement files: private application data under `Statements/`
- Cloud backup: one versioned JSONB snapshot per authenticated user in PostgreSQL
- Statement storage: private `statements` bucket, scoped by Supabase user ID
- Access and refresh tokens: platform `SecureStorage`
- Institution definitions and imported transaction data are included in backup/cloud sync.
- Local filesystem paths are never included in cloud backups.
- Split participants, shares, and settlement state are included in backup version 3.
- The publishable/anon key may be bundled in the client. Never place a Supabase `service_role` or secret key in this app.

## Supabase Setup

### 1. Create the project

1. Create a project at Supabase.
2. In Authentication, enable email/password sign-in.
3. Decide whether email confirmation is required. If enabled, configure the Site URL and email templates for the deployed app.

### 2. Apply the database migration

Apply the migrations in order through the Supabase CLI migration workflow or SQL Editor:

1. [`supabase/migrations/202608040001_initial_schema.sql`](supabase/migrations/202608040001_initial_schema.sql)
2. [`supabase/migrations/202608290001_payment_platform.sql`](supabase/migrations/202608290001_payment_platform.sql)

The migration creates:

- `public.user_backups`, keyed to `auth.users`
- Row Level Security policies that restrict every backup to its owner
- A private `statements` Storage bucket with a 10 MB per-file limit
- Storage policies that restrict statement paths to `<auth-user-id>/...`
- Owner-scoped payment accounts and payment requests
- An append-only provider-event table for webhook idempotency and reconciliation

### 3. Configure the client

Use the project URL and the publishable key from the Supabase dashboard. Configuration can be entered on the login screen, or embedded at build time.

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

The older JWT-style `anon` key is also supported. Do not use a secret key or the `service_role` key.

## Sync Behavior

- The app remains local-first; normal edits write to SQLite immediately.
- **Upload Sync** retries pending statement uploads, then upserts the current backup into PostgreSQL.
- **Download Sync** replaces the local transactions, accounts, statement metadata, goals, and schedule with the latest cloud snapshot.
- Split records use stable transaction GUIDs, so relationships survive a cross-device restore.
- Cloud backup writes are last-write-wins. Upload from the device with the desired current data before downloading on another device.
- Raw statement files remain in private Supabase Storage. Downloaded backups restore their metadata, not a device-local copy of each raw file.

## Payment Setup

Payment infrastructure is optional. The app's expense tracking, statements, and manual settlement features work without it.

- Follow [`docs/PAYMENTS.md`](docs/PAYMENTS.md) to deploy the payment migration and Edge Functions.
- Stripe card checkout is restricted to test mode and does not move real money.
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

Supabase must be configured and the migration must be applied before signup, login, or cloud sync will work.

## Product Roadmap

The next stages for Money Manager are tracked in [`docs/ROADMAP.md`](docs/ROADMAP.md). The payment roadmap deliberately separates payment requests from money movement so a future provider integration can meet security, reconciliation, identity, dispute, and app-store requirements.
