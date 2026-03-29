# ExpenseTracker (.NET MAUI)

ExpenseTracker is a cross-platform personal finance app built with .NET MAUI, SQLite, and MVVM.

## Features

- Startup auth gate:
- If an account session exists, users enter the app directly.
- If no session exists, users see login/signup first, with a `Just trying it` guest option.
- Dashboard:
- Tracks Income, Expenses, Assets, Liabilities, Net Cashflow, and Net Worth.
- Includes a 6-month net cashflow trend (line chart) and a financial composition chart (donut).
- Transactions:
- Add, edit, delete.
- Monthly view/filter.
- Goals:
- Add, edit, delete.
- Monthly view/filter by deadline month.
- Schedule:
- Add, delete.
- Monthly view/filter.
- Settings:
- Theme preference (System/Light/Dark).
- Export backup to JSON.
- Import backup from JSON.
- Account sync controls (register, sign in, sign out, upload sync, download sync).
- Theme consistency:
- Shared color/style resources are applied across screens, not just on one page.

## Architecture

- `Models/` entities and supporting DTOs/enums
- `Services/` SQLite persistence, backup, and account/cloud sync integration
- `ViewModels/` MVVM page state and filtering logic
- `Views/` XAML pages and UI interaction code-behind

## Tech Stack

- .NET 9 MAUI
- SQLite (`sqlite-net-pcl`)
- Charts (`Microcharts.Maui`)

## Data Storage

- Local DB: `expenses.db3`
- Backup format: JSON (`DataBackup`)
- Optional cloud sync via authenticated backend API

## Cloud Sync API Contract

The app expects these endpoints on your backend:

- `POST /api/account/register` body: `{ "email": "...", "password": "..." }`
- `POST /api/account/login` body: `{ "email": "...", "password": "..." }`
- Login response: `{ "token": "..." }`
- `POST /api/sync/push` body: `DataBackup` with `Authorization: Bearer <token>`
- `GET /api/sync/pull` returns: `DataBackup` with `Authorization: Bearer <token>`

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

## Notes

- Cloud sync requires setting a backend server URL in the app.
- Existing tests focus on core viewmodel filtering and dashboard aggregate logic.
