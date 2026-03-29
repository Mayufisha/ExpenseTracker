# ExpenseTracker (.NET MAUI)

ExpenseTracker is a cross-platform personal finance app built with .NET MAUI, SQLite, and MVVM.

## Features

- Dashboard with Income, Expenses, Assets, Liabilities, Net Cashflow, and Net Worth
- Dashboard charts: 6-month cashflow trend (line) and composition (donut)
- Transactions: add, edit, delete, and monthly view
- Goals: create, edit, delete, and monthly view by deadline
- Schedule: add, delete, and monthly view
- Settings:
- Theme preference (System/Light/Dark)
- Export backup to JSON
- Import backup from JSON
- Account sign-up/sign-in and cloud upload/download sync

## Architecture

- `Models/` data and enums
- `Services/` SQLite persistence, backup, and account sync services
- `ViewModels/` page logic
- `Views/` XAML pages and UI interactions

## Tech Stack

- .NET 9 MAUI
- SQLite (`sqlite-net-pcl`)
- Charts (`Microcharts.Maui`)

## Data Storage

- Local database file: `expenses.db3`
- Backup file format: JSON
- Cloud sync uses a backend API with account auth (see below)

## Cloud Sync API Contract

The app expects these authenticated endpoints on your server:

- `POST /api/account/register` with `{ "email": "...", "password": "..." }`
- `POST /api/account/login` with `{ "email": "...", "password": "..." }`
  - Response: `{ "token": "..." }`
- `POST /api/sync/push` with `DataBackup` payload and `Authorization: Bearer <token>`
- `GET /api/sync/pull` returns `DataBackup` with `Authorization: Bearer <token>`

## Getting Started

### Prerequisites

- .NET 9 SDK
- MAUI workload
- Visual Studio 2022 (or newer) with MAUI support

### Build

```bash
dotnet restore
dotnet build ExpenseTracker.sln
```

Run on a target device/emulator from Visual Studio or with MAUI CLI commands.

## Current Scope and Notes

- Cloud sync requires your backend API URL in Settings
- Test coverage focuses on viewmodel filtering and dashboard aggregates
