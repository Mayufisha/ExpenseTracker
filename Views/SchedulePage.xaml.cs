using System;
using ExpenseTracker.Models;
using ExpenseTracker.ViewModels;
using Microsoft.Maui.Controls;

namespace ExpenseTracker.Views;

public partial class SchedulePage : ContentPage
{
    private readonly ScheduleViewModel _viewModel;

    public SchedulePage(ScheduleViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await _viewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Schedule error", ex.Message, "OK");
        }
    }

    async void OnAddClicked(object sender, EventArgs e)
    {
        var note = await DisplayPromptAsync("New Scheduled Item", "Description:");
        if (string.IsNullOrWhiteSpace(note))
            return;

        var amountText = await DisplayPromptAsync("Amount", "Enter amount:", keyboard: Keyboard.Numeric);
        if (!decimal.TryParse(amountText, out var amount) || amount <= 0)
        {
            await DisplayAlert("Invalid", "Please enter a valid amount.", "OK");
            return;
        }

        var dateText = await DisplayPromptAsync("Date", "Enter date (YYYY-MM-DD):");
        if (!DateTime.TryParse(dateText, out var date))
        {
            await DisplayAlert("Invalid", "Please enter a valid date.", "OK");
            return;
        }

        await _viewModel.AddSimpleScheduleAsync(note.Trim(), amount, date);
    }

    async void OnDeleteSwipeInvoked(object sender, EventArgs e)
    {
        if (sender is not SwipeItem swipeItem) return;
        if (swipeItem.BindingContext is not ScheduledTransaction item) return;

        var confirm = await DisplayAlert(
            "Delete",
            $"Delete scheduled item \"{item.Note}\"?",
            "Yes", "No");

        if (!confirm) return;

        await _viewModel.DeleteAsync(item);
    }

    void OnFilterChanged(object sender, EventArgs e)
    {
        if (sender is not Picker picker) return;

        _viewModel.SelectedRange = picker.SelectedIndex switch
        {
            1 => TimeRange.ThisWeek,
            2 => TimeRange.ThisMonth,
            3 => TimeRange.LastMonth,
            4 => TimeRange.LastThreeMonths,
            _ => TimeRange.All
        };
    }
}
