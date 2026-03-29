# ExpenseTracker (.NET MAUI)

ExpenseTracker is a cross-platform personal finance app built with .NET MAUI, SQLite, and MVVM.

## Features

- Dashboard with Income, Expenses, Assets, Liabilities, Net Cashflow, and Net Worth
- Transactions: add, edit, delete, and filter by date range
- Goals: create and track savings goals
- Schedule: track upcoming payments
- Settings:
- Theme preference (System/Light/Dark)
- Delete all app data (transactions, goals, scheduled items)
- Export backup to JSON
- Import backup from JSON

## Architecture

- `Models/` data and enums
- `Services/` SQLite persistence and backup service
- `ViewModels/` page logic
- `Views/` XAML pages and UI interactions

## Tech Stack

- .NET 9 MAUI
- SQLite (`sqlite-net-pcl`)
- Charts (`Microcharts.Maui`)

## Data Storage

- Local database file: `expenses.db3`
- Backup file format: JSON

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

- No cloud sync yet (local-only app)
- No authentication yet
- Test coverage focuses on viewmodel filtering and dashboard aggregates
