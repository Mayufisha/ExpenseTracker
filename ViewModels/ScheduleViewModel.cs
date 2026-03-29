using System.Collections.ObjectModel;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.ViewModels;

public class ScheduleViewModel : BaseViewModel
{
    private readonly IScheduleService _scheduleService;

    public ObservableCollection<ScheduledTransaction> ScheduledItems { get; } = new();
    public ObservableCollection<MonthFilterOption> MonthFilters { get; } = new();

    private readonly List<ScheduledTransaction> _allScheduled = new();

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

    public ScheduleViewModel(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        ScheduledItems.Clear();
        _allScheduled.Clear();
        MonthFilters.Clear();

        var items = await _scheduleService.GetScheduledAsync();
        _allScheduled.AddRange(items);
        BuildMonthFilters();

        ApplyFilter();

        IsBusy = false;
    }

    private void BuildMonthFilters()
    {
        MonthFilters.Add(new MonthFilterOption { Key = "all", Label = "All Months" });

        var monthKeys = _allScheduled
            .Select(s => new DateTime(s.ScheduledDate.Year, s.ScheduledDate.Month, 1))
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
        SelectedMonthFilter = MonthFilters.FirstOrDefault(f => f.Key == currentMonthKey) ?? MonthFilters.FirstOrDefault();
    }

    private void ApplyFilter()
    {
        ScheduledItems.Clear();

        IEnumerable<ScheduledTransaction> query = _allScheduled;
        var selectedKey = SelectedMonthFilter?.Key ?? "all";
        if (!string.Equals(selectedKey, "all", StringComparison.OrdinalIgnoreCase)
            && DateTime.TryParseExact(selectedKey + "-01", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var selectedMonth))
        {
            var start = new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
            var end = start.AddMonths(1);
            query = query.Where(s => s.ScheduledDate.Date >= start && s.ScheduledDate.Date < end);
        }

        foreach (var s in query.OrderBy(s => s.ScheduledDate))
            ScheduledItems.Add(s);
    }

    public async Task AddSimpleScheduleAsync(string note, decimal amount, DateTime date)
    {
        var item = new ScheduledTransaction
        {
            Note = note,
            Amount = amount,
            ScheduledDate = date,
            IsIncome = false,
            Frequency = "None"
        };

        await _scheduleService.AddOrUpdateAsync(item);
        await LoadAsync();
    }

    public async Task DeleteAsync(ScheduledTransaction item)
    {
        if (item == null) return;

        await _scheduleService.DeleteAsync(item.Id);
        await LoadAsync();
    }
}
