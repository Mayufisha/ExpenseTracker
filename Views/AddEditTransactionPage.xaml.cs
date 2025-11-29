using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Views;

public partial class AddEditTransactionPage : ContentPage
{
    private readonly IExpenseService _expenseService;
    private Transaction _editing;
    private bool _isIncome;

    public AddEditTransactionPage(IExpenseService expenseService)
    {
        InitializeComponent();
        _expenseService = expenseService;
        _isIncome = false;
    }

    public AddEditTransactionPage(IExpenseService expenseService, Transaction existing)
        : this(expenseService)
    {
        _editing = existing;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            var categories = await _expenseService.GetCategoriesAsync();

            if (categories == null || !categories.Any())
            {
                await DisplayAlert("Error", "No categories available. Please add categories first.", "OK");
                await Navigation.PopModalAsync();
                return;
            }

            CategoryPicker.ItemsSource = categories.ToList();
            CategoryPicker.ItemDisplayBinding = new Binding("Name");

            if (_editing != null)
            {
                TitleLabel.Text = "Edit Transaction";

                AmountEntry.Text = _editing.Amount.ToString("F2");
                DatePicker.Date = _editing.Date;
                NoteEditor.Text = _editing.Note ?? string.Empty;
                _isIncome = _editing.IsIncome;
                UpdateTypeButtons();

                if (_editing.CategoryId != 0)
                {
                    var selected = categories.FirstOrDefault(c => c.Id == _editing.CategoryId);
                    if (selected != null)
                        CategoryPicker.SelectedItem = selected;
                }

                // Show delete button only when editing
                DeleteButton.IsVisible = true;
            }
            else
            {
                TitleLabel.Text = "Add Transaction";
                DatePicker.Date = DateTime.Today;
                _isIncome = false;
                UpdateTypeButtons();

                // Hide delete button when adding new
                DeleteButton.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Failed to load categories. Please try again.", "OK");
            await Navigation.PopModalAsync();
        }
    }

    void OnExpenseClicked(object sender, EventArgs e)
    {
        _isIncome = false;
        UpdateTypeButtons();
    }

    void OnIncomeClicked(object sender, EventArgs e)
    {
        _isIncome = true;
        UpdateTypeButtons();
    }

    void UpdateTypeButtons()
    {
        if (_isIncome)
        {
            IncomeButton.BackgroundColor = Color.FromArgb("#4CAF50");
            IncomeButton.TextColor = Colors.White;
            ExpenseButton.BackgroundColor = Color.FromArgb("#E0E0E0");
            ExpenseButton.TextColor = Colors.Black;
        }
        else
        {
            ExpenseButton.BackgroundColor = Color.FromArgb("#F44336");
            ExpenseButton.TextColor = Colors.White;
            IncomeButton.BackgroundColor = Color.FromArgb("#E0E0E0");
            IncomeButton.TextColor = Colors.Black;
        }
    }

    async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    async void OnSaveClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        ErrorLabel.Text = string.Empty;

        if (!decimal.TryParse(AmountEntry.Text, out var amount) || amount <= 0)
        {
            ShowError("Enter a valid amount greater than 0.");
            return;
        }

        if (CategoryPicker.SelectedItem is not Category category)
        {
            ShowError("Select a category.");
            return;
        }

        var date = DatePicker.Date;

        if (_editing == null)
        {
            _editing = new Transaction();
        }

        _editing.Amount = amount;
        _editing.IsIncome = _isIncome;
        _editing.CategoryId = category.Id;
        _editing.Date = date;
        _editing.Note = NoteEditor.Text ?? string.Empty;

        try
        {
            await _expenseService.AddOrUpdateTransactionAsync(_editing);
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            ShowError("Failed to save transaction. Please try again.");
        }
    }

    async void OnDeleteTransaction(object sender, EventArgs e)
    {
        if (_editing == null || _editing.Id == 0)
        {
            await DisplayAlert("Error", "No transaction to delete.", "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Delete",
            "Are you sure you want to delete this transaction? This cannot be undone.",
            "Yes", "No");

        if (!confirm) return;

        try
        {
            await _expenseService.DeleteTransactionAsync(_editing.Id);
            await DisplayAlert("Done", "The transaction has been deleted.", "OK");
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Failed to delete transaction. Please try again.", "OK");
        }
    }

    void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}