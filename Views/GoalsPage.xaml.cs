using ExpenseTracker.Models;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class GoalsPage : ContentPage
{
    private readonly GoalsViewModel _viewModel;

    public GoalsPage(GoalsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    async void OnAddClicked(object sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("New Goal", "Goal name:");
        if (string.IsNullOrWhiteSpace(name))
            return;

        var targetText = await DisplayPromptAsync("Target Amount", "Enter target amount:", keyboard: Keyboard.Numeric);
        if (!decimal.TryParse(targetText, out var target) || target <= 0)
        {
            await DisplayAlert("Invalid", "Please enter a valid amount.", "OK");
            return;
        }

        await _viewModel.AddSimpleGoalAsync(name.Trim(), target);
    }

    async void OnDeleteSwipeInvoked(object sender, EventArgs e)
    {
        if (sender is not SwipeItem swipeItem) return;
        if (swipeItem.BindingContext is not Goal goal) return;

        var confirm = await DisplayAlert(
            "Delete Goal",
            $"Delete goal \"{goal.Name}\"?",
            "Yes", "No");

        if (!confirm) return;

        await _viewModel.DeleteGoalAsync(goal);
    }

    async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection == null || e.CurrentSelection.Count == 0)
            return;

        var goal = e.CurrentSelection[0] as Goal;
        ((CollectionView)sender).SelectedItem = null;
        if (goal == null) return;

        var name = await DisplayPromptAsync("Edit Goal", "Goal name:", initialValue: goal.Name);
        if (string.IsNullOrWhiteSpace(name))
            return;

        var targetText = await DisplayPromptAsync(
            "Edit Goal",
            "Target amount:",
            keyboard: Keyboard.Numeric,
            initialValue: goal.TargetAmount.ToString("0.##"));
        if (!decimal.TryParse(targetText, out var target) || target <= 0)
        {
            await DisplayAlert("Invalid", "Please enter a valid target amount.", "OK");
            return;
        }

        var savedText = await DisplayPromptAsync(
            "Edit Goal",
            "Saved amount:",
            keyboard: Keyboard.Numeric,
            initialValue: goal.CurrentAmount.ToString("0.##"));
        if (!decimal.TryParse(savedText, out var saved) || saved < 0)
        {
            await DisplayAlert("Invalid", "Please enter a valid saved amount.", "OK");
            return;
        }

        var deadlineValue = goal.Deadline?.ToString("yyyy-MM-dd") ?? string.Empty;
        var deadlineText = await DisplayPromptAsync(
            "Edit Goal",
            "Deadline (YYYY-MM-DD), or leave blank:",
            initialValue: deadlineValue);

        DateTime? deadline = null;
        if (!string.IsNullOrWhiteSpace(deadlineText))
        {
            if (!DateTime.TryParse(deadlineText, out var parsedDeadline))
            {
                await DisplayAlert("Invalid", "Please enter a valid date.", "OK");
                return;
            }

            deadline = parsedDeadline.Date;
        }

        await _viewModel.UpdateGoalAsync(goal, name.Trim(), target, saved, deadline);
    }
}
