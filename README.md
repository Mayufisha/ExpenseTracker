# 📌 Expense Tracker – .NET MAUI

A modern cross-platform expense tracking application built with .NET MAUI, SQLite, MVVM, and Microcharts.

## ✨ Features

### 🔹 Dashboard
* Displays Total Income, Total Expenses, and Balance
* Includes a vertical bar chart (Microcharts)
* Shows recent transactions

### 🔹 Transactions
* Add, edit, and delete transactions
* Swipe-to-delete support
* Filter by:
   * All
   * This Week
   * This Month
   * Last Month
   * Last 3 Months

### 🔹 Goals
* Create and track savings goals
* Dynamic progress calculation
* Deadline support
* Swipe-to-delete

### 🔹 Schedule
* Add upcoming or recurring payments
* Filter by date range
* Swipe-to-delete

### 🔹 Settings
* Delete all transactions
* (Expandable for future features)

## 🏛 Architecture

The application uses the MVVM (Model–View–ViewModel) pattern:

### Models
* `Transaction`
* `Category`
* `Goal`
* `ScheduledTransaction`
* `TimeRange` (shared filter enum)

### Services
* `SQLiteExpenseService`
* `SQLiteGoalService`
* `SQLiteScheduleService`

### ViewModels
* `DashboardViewModel`
* `TransactionsViewModel`
* `GoalsViewModel`
* `ScheduleViewModel`

### Views
* `DashboardPage`
* `TransactionsPage`
* `GoalsPage`
* `SchedulePage`
* `SettingsPage`

## 📊 Charts (Microcharts)

Dashboard uses Microcharts.Maui to render a clean bar chart:
* Income
* Expenses
* Balance

Configured in `DashboardPage.xaml.cs`.

## 🗂 Database

SQLite database stored locally:
```
expenses.db3
```

Tables are created automatically:
* Transactions
* Goals
* Scheduled transactions

## 🚀 Getting Started

### Prerequisites
* .NET 8 or .NET 9
* Visual Studio 2022 with MAUI workload
* Windows / macOS / Android device or emulator

### Installation

Clone the repo:
```bash
git clone https://github.com/yourusername/ExpenseTracker.git
cd ExpenseTracker
```

Restore packages:
```bash
dotnet restore
```

Run the project:
```bash
dotnet maui run
```

## 📦 NuGet Packages Used
```text
Microcharts.Maui
SQLite-net-pcl
CommunityToolkit.Mvvm
```

## 📝 Future Improvements
* Category-based pie chart
* Monthly trend line graph
* Export to CSV / PDF
* User authentication
* Cloud sync options

## 📄 License

This project is for educational purposes as part of the Vancouver Community College CST .NET MAUI Final Project.
- First Wireframe Design  
  [📄 View Desktop UI Wireframe](./Desktop%20UI.pdf)
