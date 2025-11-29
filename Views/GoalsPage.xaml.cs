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
}
