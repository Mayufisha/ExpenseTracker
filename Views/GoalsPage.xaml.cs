using System.Globalization;
using ExpenseTracker.Models;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class GoalsPage : ContentPage
{
    private readonly GoalsViewModel _viewModel;
    private Goal? _editingGoal;

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

    private void OnAddClicked(object sender, EventArgs e) => ShowEditor(null);

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0) return;
        var goal = e.CurrentSelection[0] as Goal;
        ((CollectionView)sender).SelectedItem = null;
        if (goal != null) ShowEditor(goal);
    }

    private async void OnSaveGoalClicked(object sender, EventArgs e)
    {
        var name = GoalNameEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name)
            || !TryParseAmount(GoalTargetEntry.Text, out var target)
            || target <= 0
            || !TryParseAmount(GoalSavedEntry.Text, out var saved)
            || saved < 0)
        {
            ShowError("Enter a name, a target above zero, and a saved amount of zero or more.");
            return;
        }

        var goal = _editingGoal ?? new Goal();
        await _viewModel.UpdateGoalAsync(
            goal,
            name,
            target,
            saved,
            GoalDeadlineSwitch.IsToggled ? GoalDeadlinePicker.Date.Date : null);
        GoalStatusLabel.Text = $"Saved {name}.";
        HideEditor();
    }

    private void OnCancelGoalClicked(object sender, EventArgs e) => HideEditor();

    private void OnGoalDeadlineToggled(object sender, ToggledEventArgs e) =>
        GoalDeadlinePicker.IsEnabled = e.Value;

    private async void OnDeleteSwipeInvoked(object sender, EventArgs e)
    {
        if (sender is not SwipeItem { BindingContext: Goal goal }) return;
        var confirm = await DisplayAlert("Delete Goal", $"Delete goal \"{goal.Name}\"?", "Delete", "Cancel");
        if (!confirm) return;

        await _viewModel.DeleteGoalAsync(goal);
        GoalStatusLabel.Text = $"Deleted {goal.Name}.";
        if (_editingGoal?.Id == goal.Id) HideEditor();
    }

    private void ShowEditor(Goal? goal)
    {
        _editingGoal = goal;
        GoalEditorTitle.Text = goal == null ? "Add goal" : "Edit goal";
        GoalNameEntry.Text = goal?.Name ?? string.Empty;
        GoalTargetEntry.Text = goal?.TargetAmount.ToString("0.##", CultureInfo.CurrentCulture) ?? string.Empty;
        GoalSavedEntry.Text = goal?.CurrentAmount.ToString("0.##", CultureInfo.CurrentCulture) ?? "0";
        GoalDeadlineSwitch.IsToggled = goal?.Deadline.HasValue == true;
        GoalDeadlinePicker.Date = goal?.Deadline?.Date ?? DateTime.Today.AddMonths(1);
        GoalDeadlinePicker.IsEnabled = GoalDeadlineSwitch.IsToggled;
        GoalEditorError.IsVisible = false;
        GoalEditor.IsVisible = true;
        GoalNameEntry.Focus();
    }

    private void HideEditor()
    {
        _editingGoal = null;
        GoalEditor.IsVisible = false;
        GoalEditorError.IsVisible = false;
    }

    private void ShowError(string message)
    {
        GoalEditorError.Text = message;
        GoalEditorError.IsVisible = true;
    }

    private static bool TryParseAmount(string? text, out decimal amount) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount)
        || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
}
