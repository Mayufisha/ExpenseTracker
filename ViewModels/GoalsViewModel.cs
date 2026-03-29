using System.Collections.ObjectModel;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.ViewModels;

public class GoalsViewModel : BaseViewModel
{
    private readonly IGoalService _goalService;

    public ObservableCollection<Goal> Goals { get; } = new();
    public ObservableCollection<MonthFilterOption> MonthFilters { get; } = new();
    private readonly List<Goal> _allGoals = new();

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

    public GoalsViewModel(IGoalService goalService)
    {
        _goalService = goalService;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        Goals.Clear();
        _allGoals.Clear();
        MonthFilters.Clear();

        var items = await _goalService.GetGoalsAsync();
        _allGoals.AddRange(items);
        BuildMonthFilters();
        ApplyFilter();

        IsBusy = false;
    }

    private void BuildMonthFilters()
    {
        MonthFilters.Add(new MonthFilterOption { Key = "all", Label = "All Goals" });
        MonthFilters.Add(new MonthFilterOption { Key = "no-deadline", Label = "No Deadline" });

        var months = _allGoals
            .Where(g => g.Deadline.HasValue)
            .Select(g => new DateTime(g.Deadline!.Value.Year, g.Deadline.Value.Month, 1))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        foreach (var month in months)
        {
            MonthFilters.Add(new MonthFilterOption
            {
                Key = month.ToString("yyyy-MM"),
                Label = month.ToString("MMMM yyyy")
            });
        }

        var currentMonthKey = DateTime.Today.ToString("yyyy-MM");
        SelectedMonthFilter = MonthFilters.FirstOrDefault(m => m.Key == currentMonthKey) ?? MonthFilters.FirstOrDefault();
    }

    private void ApplyFilter()
    {
        Goals.Clear();

        IEnumerable<Goal> query = _allGoals;
        var selectedKey = SelectedMonthFilter?.Key ?? "all";

        if (string.Equals(selectedKey, "no-deadline", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(g => !g.Deadline.HasValue);
        }
        else if (!string.Equals(selectedKey, "all", StringComparison.OrdinalIgnoreCase)
                 && DateTime.TryParseExact(selectedKey + "-01", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var selectedMonth))
        {
            var start = new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
            var end = start.AddMonths(1);
            query = query.Where(g => g.Deadline.HasValue && g.Deadline.Value.Date >= start && g.Deadline.Value.Date < end);
        }

        foreach (var goal in query.OrderBy(g => g.Deadline ?? DateTime.MaxValue))
        {
            Goals.Add(goal);
        }
    }

    public async Task AddSimpleGoalAsync(string name, decimal target)
    {
        var goal = new Goal
        {
            Name = name,
            TargetAmount = target,
            CurrentAmount = 0,
            IsCompleted = false
        };

        await _goalService.AddOrUpdateGoalAsync(goal);
        await LoadAsync();
    }

    public async Task DeleteGoalAsync(Goal goal)
    {
        if (goal == null) return;
        await _goalService.DeleteGoalAsync(goal.Id);
        await LoadAsync();
    }

    public async Task UpdateGoalAsync(Goal goal, string name, decimal target, decimal saved, DateTime? deadline)
    {
        goal.Name = name;
        goal.TargetAmount = target;
        goal.CurrentAmount = saved;
        goal.Deadline = deadline;
        goal.IsCompleted = saved >= target && target > 0;

        await _goalService.AddOrUpdateGoalAsync(goal);
        await LoadAsync();
    }
}
