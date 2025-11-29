using System.Collections.ObjectModel;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.ViewModels;

public class ScheduleViewModel : BaseViewModel
{
    private readonly IScheduleService _scheduleService;

    public ObservableCollection<ScheduledTransaction> ScheduledItems { get; } = new();

    public ScheduleViewModel(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        ScheduledItems.Clear();
        var items = await _scheduleService.GetScheduledAsync();
        foreach (var s in items)
            ScheduledItems.Add(s);

        IsBusy = false;
    }

    public async Task AddSimpleScheduleAsync(string note, decimal amount, DateTime date)
    {
        var item = new ScheduledTransaction
        {
            Note = note,
            Amount = amount,
            ScheduledDate = date,
            IsIncome = false
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
