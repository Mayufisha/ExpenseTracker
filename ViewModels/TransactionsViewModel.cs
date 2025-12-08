using System.Collections.ObjectModel;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.ViewModels;

public class TransactionsViewModel : BaseViewModel
{
    private readonly IExpenseService _expenseService;

    public ObservableCollection<Transaction> Transactions { get; } = new();

    private List<Transaction> _allTransactions = new();   

    private TimeRange _selectedRange = TimeRange.All;
    public TimeRange SelectedRange
    {
        get => _selectedRange;
        set
        {
            if (_selectedRange == value) return;
            _selectedRange = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    public TransactionsViewModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        Transactions.Clear();
        _allTransactions.Clear();

        var items = await _expenseService.GetTransactionsAsync();
        _allTransactions = items.ToList();

        ApplyFilter();

        IsBusy = false;
    }

    void ApplyFilter()
    {
        Transactions.Clear();

        var today = DateTime.Today;
        DateTime start;

        IEnumerable<Transaction> query = _allTransactions;

        switch (SelectedRange)
        {
            case TimeRange.ThisWeek:
                int diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                start = today.AddDays(-diff);
                query = query.Where(t => t.Date.Date >= start);
                break;

            case TimeRange.ThisMonth:
                start = new DateTime(today.Year, today.Month, 1);
                query = query.Where(t => t.Date.Date >= start);
                break;

            case TimeRange.LastMonth:
                var lastMonthDate = today.AddMonths(-1);
                start = new DateTime(lastMonthDate.Year, lastMonthDate.Month, 1);
                var endLast = start.AddMonths(1);
                query = query.Where(t => t.Date.Date >= start && t.Date.Date < endLast);
                break;

            case TimeRange.LastThreeMonths:
                start = today.AddMonths(-3);
                query = query.Where(t => t.Date.Date >= start);
                break;

            case TimeRange.All:
            default:
                break;
        }

        foreach (var t in query.OrderByDescending(t => t.Date))
            Transactions.Add(t);
    }
}
