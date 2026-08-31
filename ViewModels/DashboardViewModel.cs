using System.Collections.ObjectModel;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private readonly IExpenseService _expenseService;
    private readonly IFinancialAccountService _financialAccountService;
    private readonly IStatementImportService? _statementImportService;

    public ObservableCollection<Transaction> Transactions { get; } = new();
    public IReadOnlyList<MonthlyNetPoint> MonthlyNetPoints { get; private set; } = Array.Empty<MonthlyNetPoint>();

    decimal totalIncome;
    public decimal TotalIncome
    {
        get => totalIncome;
        set { totalIncome = value; OnPropertyChanged(); }
    }

    decimal totalExpense;
    public decimal TotalExpense
    {
        get => totalExpense;
        set { totalExpense = value; OnPropertyChanged(); }
    }

    decimal totalAssets;
    public decimal TotalAssets
    {
        get => totalAssets;
        set { totalAssets = value; OnPropertyChanged(); }
    }

    decimal totalLiabilities;
    public decimal TotalLiabilities
    {
        get => totalLiabilities;
        set { totalLiabilities = value; OnPropertyChanged(); }
    }

    decimal netCashFlow;
    public decimal NetCashFlow
    {
        get => netCashFlow;
        set { netCashFlow = value; OnPropertyChanged(); }
    }

    decimal netWorth;
    public decimal NetWorth
    {
        get => netWorth;
        set { netWorth = value; OnPropertyChanged(); }
    }

    string netWorthStatus = string.Empty;
    public string NetWorthStatus
    {
        get => netWorthStatus;
        set { netWorthStatus = value; OnPropertyChanged(); }
    }

    public DashboardViewModel(
        IExpenseService expenseService,
        IFinancialAccountService financialAccountService,
        IStatementImportService? statementImportService = null)
    {
        _expenseService = expenseService;
        _financialAccountService = financialAccountService;
        _statementImportService = statementImportService;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        Transactions.Clear();
        if (_statementImportService != null)
            await _statementImportService.ReclassifyAttachedTransactionsAsync();

        var items = await _expenseService.GetTransactionsAsync();
        var accounts = await _financialAccountService.GetAccountsAsync();

        foreach (var t in items)
            Transactions.Add(t);

        TotalIncome = Transactions.Where(t => t.ParsedType == TransactionType.Income).Sum(t => t.Amount);
        TotalExpense = Transactions.Where(t => t.ParsedType == TransactionType.Expense).Sum(t => t.Amount);
        TotalAssets = Transactions.Where(t => t.ParsedType == TransactionType.Asset).Sum(t => t.Amount)
            + accounts.Sum(account => account.AssetValue);
        TotalLiabilities = Transactions.Where(t => t.ParsedType == TransactionType.Liability).Sum(t => t.Amount)
            + accounts.Sum(account => account.LiabilityValue);

        NetCashFlow = TotalIncome - TotalExpense;
        NetWorth = TotalAssets - TotalLiabilities;
        var missingBalanceCount = accounts.Count(account => !account.CurrentBalance.HasValue);
        NetWorthStatus = accounts.Count == 0
            ? "Add financial accounts and their current balances to calculate net worth."
            : missingBalanceCount > 0
                ? $"Set the current balance for {missingBalanceCount} account{(missingBalanceCount == 1 ? string.Empty : "s")} to complete net worth. Statements alone only show cash flow."
                : $"Based on {accounts.Count} account balance{(accounts.Count == 1 ? string.Empty : "s")} plus manually tracked assets and liabilities.";
        MonthlyNetPoints = BuildMonthlyNetPoints(Transactions, 6);
        OnPropertyChanged(nameof(MonthlyNetPoints));

        IsBusy = false;
    }

    private static IReadOnlyList<MonthlyNetPoint> BuildMonthlyNetPoints(IEnumerable<Transaction> transactions, int monthCount)
    {
        var months = Enumerable.Range(0, monthCount)
            .Select(offset => DateTime.Today.AddMonths(-monthCount + 1 + offset))
            .Select(d => new DateTime(d.Year, d.Month, 1))
            .ToList();

        var result = new List<MonthlyNetPoint>(monthCount);
        foreach (var month in months)
        {
            var end = month.AddMonths(1);
            var monthItems = transactions.Where(t => t.Date.Date >= month && t.Date.Date < end).ToList();
            var income = monthItems.Where(t => t.ParsedType == TransactionType.Income).Sum(t => t.Amount);
            var expense = monthItems.Where(t => t.ParsedType == TransactionType.Expense).Sum(t => t.Amount);
            result.Add(new MonthlyNetPoint
            {
                Label = month.ToString("MMM"),
                NetCashFlow = income - expense
            });
        }

        return result;
    }
}
