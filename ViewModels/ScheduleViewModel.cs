using System.Collections.ObjectModel;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.ViewModels;

public class ScheduleViewModel : BaseViewModel
{
    private readonly IScheduleService _scheduleService;

    public ObservableCollection<ScheduledTransaction> ScheduledItems { get; } = new();

    private List<ScheduledTransaction> _allScheduled = new();

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

        var items = await _scheduleService.GetScheduledAsync();
        _allScheduled = items.ToList();

        ApplyFilter();

        IsBusy = false;
    }

    private void ApplyFilter()
    {
        ScheduledItems.Clear();

        var today = DateTime.Today;
        DateTime start;
        IEnumerable<ScheduledTransaction> query = _allScheduled;

        switch (SelectedRange)
        {
            case TimeRange.ThisWeek:
                int diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                start = today.AddDays(-diff);
                query = query.Where(s => s.ScheduledDate.Date >= start);
                break;

            case TimeRange.ThisMonth:
                start = new DateTime(today.Year, today.Month, 1);
                query = query.Where(s => s.ScheduledDate.Date >= start);
                break;

            case TimeRange.LastMonth:
                var lastMonthDate = today.AddMonths(-1);
                start = new DateTime(lastMonthDate.Year, lastMonthDate.Month, 1);
                var endLast = start.AddMonths(1);
                query = query.Where(s => s.ScheduledDate.Date >= start &&
                                         s.ScheduledDate.Date < endLast);
                break;

            case TimeRange.LastThreeMonths:
                start = today.AddMonths(-3);
                query = query.Where(s => s.ScheduledDate.Date >= start);
                break;

            case TimeRange.All:
            default:
                break;
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
