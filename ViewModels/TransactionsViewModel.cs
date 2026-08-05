using System.Collections.ObjectModel;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.ViewModels;

public class TransactionsViewModel : BaseViewModel
{
    private readonly IExpenseService _expenseService;
    private readonly List<Transaction> _allTransactions = new();

    public ObservableCollection<Transaction> Transactions { get; } = new();
    public ObservableCollection<MonthFilterOption> MonthFilters { get; } = new();
    public ObservableCollection<string> InstitutionFilters { get; } = new();

    private MonthFilterOption? _selectedMonthFilter;
    public MonthFilterOption? SelectedMonthFilter
    {
        get => _selectedMonthFilter;
        set
        {
            if (_selectedMonthFilter?.Key == value?.Key) return;
            _selectedMonthFilter = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    private string _selectedInstitution = "All Institutions";
    public string SelectedInstitution
    {
        get => _selectedInstitution;
        set
        {
            if (_selectedInstitution == value) return;
            _selectedInstitution = value;
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
        MonthFilters.Clear();
        InstitutionFilters.Clear();

        var items = await _expenseService.GetTransactionsAsync();
        _allTransactions.AddRange(items);

        BuildInstitutionFilters();
        BuildMonthFilters();
        ApplyFilter();

        IsBusy = false;
    }

    private void BuildInstitutionFilters()
    {
        InstitutionFilters.Add("All Institutions");
        InstitutionFilters.Add("Manual Entries");

        foreach (var institution in _allTransactions
                     .Where(t => !string.IsNullOrWhiteSpace(t.InstitutionName))
                     .Select(t => t.InstitutionName)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(name => name))
        {
            InstitutionFilters.Add(institution);
        }

        if (!InstitutionFilters.Contains(SelectedInstitution))
            SelectedInstitution = "All Institutions";
    }

    private void BuildMonthFilters()
    {
        MonthFilters.Add(new MonthFilterOption { Key = "all", Label = "All Months" });

        var monthKeys = _allTransactions
            .Select(t => new DateTime(t.Date.Year, t.Date.Month, 1))
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        foreach (var month in monthKeys)
        {
            MonthFilters.Add(new MonthFilterOption
            {
                Key = month.ToString("yyyy-MM"),
                Label = month.ToString("MMMM yyyy")
            });
        }

        var currentMonthKey = DateTime.Today.ToString("yyyy-MM");
        SelectedMonthFilter = MonthFilters.FirstOrDefault(f => f.Key == currentMonthKey)
            ?? MonthFilters.FirstOrDefault();
    }

    private void ApplyFilter()
    {
        Transactions.Clear();

        IEnumerable<Transaction> query = _allTransactions;
        if (SelectedInstitution == "Manual Entries")
        {
            query = query.Where(t => string.IsNullOrWhiteSpace(t.InstitutionName));
        }
        else if (SelectedInstitution != "All Institutions")
        {
            query = query.Where(t => t.InstitutionName.Equals(
                SelectedInstitution,
                StringComparison.OrdinalIgnoreCase));
        }

        var selectedKey = SelectedMonthFilter?.Key ?? "all";
        if (!string.Equals(selectedKey, "all", StringComparison.OrdinalIgnoreCase)
            && DateTime.TryParseExact(
                selectedKey + "-01",
                "yyyy-MM-dd",
                null,
                System.Globalization.DateTimeStyles.None,
                out var selectedMonth))
        {
            var start = new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
            var end = start.AddMonths(1);
            query = query.Where(t => t.Date.Date >= start && t.Date.Date < end);
        }

        foreach (var transaction in query.OrderByDescending(t => t.Date))
            Transactions.Add(transaction);
    }
}
