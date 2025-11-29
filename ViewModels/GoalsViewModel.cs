using System.Collections.ObjectModel;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.ViewModels;

public class GoalsViewModel : BaseViewModel
{
    private readonly IGoalService _goalService;

    public ObservableCollection<Goal> Goals { get; } = new();

    public GoalsViewModel(IGoalService goalService)
    {
        _goalService = goalService;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        Goals.Clear();
        var items = await _goalService.GetGoalsAsync();
        foreach (var g in items)
            Goals.Add(g);

        IsBusy = false;
    }
}
