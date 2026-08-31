using System.Collections.ObjectModel;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private readonly IExpenseService _expenseService;
    private readonly IFinancialAccountService _financialAccountService;
    private readonly IStatementImportService? _statementImportService;
    private readonly List<Transaction> _allTransactions = new();
    private IReadOnlyList<FinancialAccount> _accounts = Array.Empty<FinancialAccount>();

    public ObservableCollection<Transaction> Transactions { get; } = new();
    public ObservableCollection<MonthFilterOption> MonthFilters { get; } = new();
    public IReadOnlyList<int> TrendRangeOptions { get; } = [3, 6, 12];
    public IReadOnlyList<MonthlyNetPoint> MonthlyNetPoints { get; private set; } = Array.Empty<MonthlyNetPoint>();

    private int _selectedTrendMonths = 6;
    public int SelectedTrendMonths
    {
        get => _selectedTrendMonths;
        set
        {
            if (_selectedTrendMonths == value || !TrendRangeOptions.Contains(value)) return;
            _selectedTrendMonths = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TrendTitle));
            RefreshTrendPoints();
        }
    }

    public string TrendTitle => $"{SelectedTrendMonths}-Month Net Cash Flow Trend";

    private MonthFilterOption? _selectedMonthFilter;
    public MonthFilterOption? SelectedMonthFilter
    {
        get => _selectedMonthFilter;
        set
        {
            if (ReferenceEquals(_selectedMonthFilter, value)) return;
            _selectedMonthFilter = value;
            OnPropertyChanged();
            ApplySelectedMonth();
        }
    }

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

        try
        {
            Transactions.Clear();
            _allTransactions.Clear();
            MonthFilters.Clear();
            if (_statementImportService != null)
                await _statementImportService.ReclassifyAttachedTransactionsAsync();

            _allTransactions.AddRange(await _expenseService.GetTransactionsAsync());
            _accounts = await _financialAccountService.GetAccountsAsync();
            BuildMonthFilters();
            RefreshTrendPoints();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BuildMonthFilters()
    {
        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var months = _allTransactions
            .Select(transaction => new DateTime(transaction.Date.Year, transaction.Date.Month, 1))
            .Append(currentMonth)
            .Distinct()
            .OrderByDescending(month => month);

        foreach (var month in months)
        {
            MonthFilters.Add(new MonthFilterOption
            {
                Key = month.ToString("yyyy-MM"),
                Label = month.ToString("MMMM yyyy")
            });
        }

        var selectedKey = SelectedMonthFilter?.Key ?? currentMonth.ToString("yyyy-MM");
        SelectedMonthFilter = MonthFilters.FirstOrDefault(option => option.Key == selectedKey)
            ?? MonthFilters.First(option => option.Key == currentMonth.ToString("yyyy-MM"));
    }

    private void ApplySelectedMonth()
    {
        if (!TryGetSelectedMonth(out var start)) return;
        var end = start.AddMonths(1);
        var monthlyTransactions = _allTransactions
            .Where(transaction => transaction.Date.Date >= start && transaction.Date.Date < end)
            .OrderByDescending(transaction => transaction.Date)
            .ToList();

        Transactions.Clear();
        foreach (var transaction in monthlyTransactions)
            Transactions.Add(transaction);

        TotalIncome = monthlyTransactions
            .Where(transaction => transaction.ParsedType == TransactionType.Income)
            .Sum(transaction => transaction.Amount);
        TotalExpense = monthlyTransactions
            .Where(transaction => transaction.ParsedType == TransactionType.Expense)
            .Sum(transaction => transaction.Amount);
        TotalAssets = _allTransactions
            .Where(transaction => transaction.ParsedType == TransactionType.Asset)
            .Sum(transaction => transaction.Amount)
            + _accounts.Sum(account => account.AssetValue);
        TotalLiabilities = _allTransactions
            .Where(transaction => transaction.ParsedType == TransactionType.Liability)
            .Sum(transaction => transaction.Amount)
            + _accounts.Sum(account => account.LiabilityValue);
        NetCashFlow = TotalIncome - TotalExpense;
        NetWorth = TotalAssets - TotalLiabilities;

        var missingBalanceCount = _accounts.Count(account => !account.CurrentBalance.HasValue);
        NetWorthStatus = _accounts.Count == 0
            ? "Add financial accounts and their current balances to calculate net worth."
            : missingBalanceCount > 0
                ? $"Set the current balance for {missingBalanceCount} account{(missingBalanceCount == 1 ? string.Empty : "s")} to complete net worth. Statements alone only show cash flow."
                : $"Based on {_accounts.Count} account balance{(_accounts.Count == 1 ? string.Empty : "s")} plus manually tracked assets and liabilities.";
    }

    private bool TryGetSelectedMonth(out DateTime month)
    {
        var parsed = DateTime.TryParseExact(
            $"{SelectedMonthFilter?.Key}-01",
            "yyyy-MM-dd",
            null,
            System.Globalization.DateTimeStyles.None,
            out month);
        month = parsed
            ? new DateTime(month.Year, month.Month, 1)
            : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        return true;
    }

    private void RefreshTrendPoints()
    {
        MonthlyNetPoints = BuildMonthlyNetPoints(_allTransactions, SelectedTrendMonths);
        OnPropertyChanged(nameof(MonthlyNetPoints));
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
                Label = month.ToString(monthCount > 6 ? "MMM yy" : "MMM"),
                NetCashFlow = income - expense
            });
        }

        return result;
    }
}
