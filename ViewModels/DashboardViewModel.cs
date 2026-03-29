using System.Collections.ObjectModel;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private readonly IExpenseService _expenseService;

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

    public DashboardViewModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        Transactions.Clear();
        var items = await _expenseService.GetTransactionsAsync();

        foreach (var t in items)
            Transactions.Add(t);

        TotalIncome = Transactions.Where(t => t.ParsedType == TransactionType.Income).Sum(t => t.Amount);
        TotalExpense = Transactions.Where(t => t.ParsedType == TransactionType.Expense).Sum(t => t.Amount);
        TotalAssets = Transactions.Where(t => t.ParsedType == TransactionType.Asset).Sum(t => t.Amount);
        TotalLiabilities = Transactions.Where(t => t.ParsedType == TransactionType.Liability).Sum(t => t.Amount);

        NetCashFlow = TotalIncome - TotalExpense;
        NetWorth = TotalAssets - TotalLiabilities;
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
