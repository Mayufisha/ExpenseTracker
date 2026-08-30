using System.Globalization;
using ExpenseTracker.Models;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class SchedulePage : ContentPage
{
    private readonly ScheduleViewModel _viewModel;
    private ScheduledTransaction? _editingItem;

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
        catch (Exception exception)
        {
            ScheduleStatusLabel.Text = $"Schedule error: {exception.Message}";
        }
    }

    private void OnAddClicked(object sender, EventArgs e) => ShowEditor(null);

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0) return;
        var item = e.CurrentSelection[0] as ScheduledTransaction;
        ((CollectionView)sender).SelectedItem = null;
        if (item != null) ShowEditor(item);
    }

    private async void OnSaveScheduleClicked(object sender, EventArgs e)
    {
        var note = ScheduleNoteEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(note)
            || !TryParseAmount(ScheduleAmountEntry.Text, out var amount)
            || amount <= 0)
        {
            ShowError("Enter a description and an amount above zero.");
            return;
        }

        if (ScheduleTypePicker.SelectedIndex < 0 || ScheduleFrequencyPicker.SelectedIndex < 0)
        {
            ShowError("Choose a type and frequency.");
            return;
        }

        var frequency = ScheduleFrequencyPicker.SelectedIndex switch
        {
            1 => "Weekly",
            2 => "Monthly",
            _ => "None"
        };
        await _viewModel.SaveScheduleAsync(
            _editingItem,
            note,
            amount,
            ScheduleDatePicker.Date.Date,
            ScheduleTypePicker.SelectedIndex == 1,
            frequency);
        ScheduleStatusLabel.Text = $"Saved {note}.";
        HideEditor();
    }

    private void OnCancelScheduleClicked(object sender, EventArgs e) => HideEditor();

    private async void OnDeleteSwipeInvoked(object sender, EventArgs e)
    {
        if (sender is not SwipeItem { BindingContext: ScheduledTransaction item }) return;
        var confirm = await DisplayAlert(
            "Delete Scheduled Item",
            $"Delete scheduled item \"{item.Note}\"?",
            "Delete",
            "Cancel");
        if (!confirm) return;

        await _viewModel.DeleteAsync(item);
        ScheduleStatusLabel.Text = $"Deleted {item.Note}.";
        if (_editingItem?.Id == item.Id) HideEditor();
    }

    private void ShowEditor(ScheduledTransaction? item)
    {
        _editingItem = item;
        ScheduleEditorTitle.Text = item == null ? "Add scheduled item" : "Edit scheduled item";
        ScheduleNoteEntry.Text = item?.Note ?? string.Empty;
        ScheduleAmountEntry.Text = item?.Amount.ToString("0.##", CultureInfo.CurrentCulture) ?? string.Empty;
        ScheduleDatePicker.Date = item?.ScheduledDate.Date ?? DateTime.Today;
        ScheduleTypePicker.SelectedIndex = item?.IsIncome == true ? 1 : 0;
        ScheduleFrequencyPicker.SelectedIndex = item?.Frequency switch
        {
            "Weekly" => 1,
            "Monthly" => 2,
            _ => 0
        };
        ScheduleEditorError.IsVisible = false;
        ScheduleEditor.IsVisible = true;
        ScheduleNoteEntry.Focus();
    }

    private void HideEditor()
    {
        _editingItem = null;
        ScheduleEditor.IsVisible = false;
        ScheduleEditorError.IsVisible = false;
    }

    private void ShowError(string message)
    {
        ScheduleEditorError.Text = message;
        ScheduleEditorError.IsVisible = true;
    }

    private static bool TryParseAmount(string? text, out decimal amount) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount)
        || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
}
