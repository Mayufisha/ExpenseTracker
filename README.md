# ExpenseTracker (.NET MAUI)

ExpenseTracker is a cross-platform personal finance app built with .NET MAUI, SQLite, and MVVM.

## Features

### Authentication and Sync

- Login or signup is required before financial data can be accessed.
- The active session is saved locally and restored when the app starts again.
- Users can sign out from Settings.
- Account data can be uploaded to or downloaded from a configured sync server.

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
- PDF statements are stored in the app's private data directory for reference.
- Duplicate statement files are detected using a SHA-256 file hash.

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
- Charts (`Microcharts.Maui`)

## Data Storage and Privacy

- Local database: `expenses.db3`
- Statement files: private application data under `Statements/`
- Backup format: JSON (`DataBackup`)
- Institution definitions and imported transaction data are included in backup/cloud sync.
- Raw statement files are kept local and are not uploaded by the current sync implementation.

## Cloud Sync API Contract

The app expects these endpoints on the configured backend:

- `POST /api/account/register` body: `{ "email": "...", "password": "..." }`
- `POST /api/account/login` body: `{ "email": "...", "password": "..." }`
- Login response: `{ "token": "..." }`
- `POST /api/sync/push` body: `DataBackup`, authorized with `Bearer <token>`
- `GET /api/sync/pull` returns `DataBackup`, authorized with `Bearer <token>`

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

Cloud sync requires a compatible backend URL configured on the login screen.
