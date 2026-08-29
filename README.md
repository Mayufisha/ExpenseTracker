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
- `Services/` SQLite persistence, statement parsing/import, backup, and account sync
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
- The publishable/anon key may be bundled in the client. Never place a Supabase `service_role` or secret key in this app.

## Supabase Setup

### 1. Create the project

1. Create a project at Supabase.
2. In Authentication, enable email/password sign-in.
3. Decide whether email confirmation is required. If enabled, configure the Site URL and email templates for the deployed app.

### 2. Apply the database migration

Run [`supabase/migrations/202608040001_initial_schema.sql`](supabase/migrations/202608040001_initial_schema.sql) once in the Supabase SQL Editor, or apply it with the Supabase CLI migration workflow.

The migration creates:

- `public.user_backups`, keyed to `auth.users`
- Row Level Security policies that restrict every backup to its owner
- A private `statements` Storage bucket with a 10 MB per-file limit
- Storage policies that restrict statement paths to `<auth-user-id>/...`

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
- Cloud backup writes are last-write-wins. Upload from the device with the desired current data before downloading on another device.
- Raw statement files remain in private Supabase Storage. Downloaded backups restore their metadata, not a device-local copy of each raw file.

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
