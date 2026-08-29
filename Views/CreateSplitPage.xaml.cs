using System.Globalization;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Views;

public partial class CreateSplitPage : ContentPage
{
    private readonly IExpenseService _expenseService;
    private readonly ISplitService _splitService;
    private readonly Transaction? _preselectedTransaction;
    private bool _loaded;

    public event EventHandler? SplitCreated;

    public CreateSplitPage(
        IExpenseService expenseService,
        ISplitService splitService,
        Transaction? preselectedTransaction = null)
    {
        InitializeComponent();
        _expenseService = expenseService;
        _splitService = splitService;
        _preselectedTransaction = preselectedTransaction;
        MethodPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;

        var existingTransactionIds = (await _splitService.GetSplitsAsync())
            .Select(split => split.TransactionSyncId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var transactions = (await _expenseService.GetTransactionsAsync())
            .Where(transaction => transaction.ParsedType == TransactionType.Expense)
            .Where(transaction => !existingTransactionIds.Contains(transaction.SyncId)
                                  || transaction.SyncId == _preselectedTransaction?.SyncId)
            .OrderByDescending(transaction => transaction.Date)
            .ToList();

        TransactionPicker.ItemsSource = transactions;
        if (_preselectedTransaction != null)
        {
            TransactionPicker.SelectedItem = transactions.FirstOrDefault(transaction =>
                transaction.SyncId == _preselectedTransaction.SyncId);
        }
    }

    private void OnTransactionChanged(object sender, EventArgs e)
    {
        if (TransactionPicker.SelectedItem is not Transaction transaction) return;
        if (string.IsNullOrWhiteSpace(TitleEntry.Text))
            TitleEntry.Text = transaction.Note;
        UpdatePreview();
    }

    private void OnMethodChanged(object sender, EventArgs e)
    {
        CustomAmountsSection.IsVisible = MethodPicker.SelectedItem?.ToString() == "Custom";
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (TransactionPicker.SelectedItem is not Transaction transaction)
        {
            PreviewLabel.Text = "Choose a transaction to preview the split.";
            return;
        }

        PreviewLabel.Text = $"Transaction total: ${transaction.Amount:F2}. " +
                            "Add people, then create the split to calculate your share.";
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            SaveButton.IsEnabled = false;
            if (TransactionPicker.SelectedItem is not Transaction transaction)
                throw new InvalidOperationException("Choose an expense transaction.");

            var people = ParsePeople(PeopleEditor.Text);
            var method = MethodPicker.SelectedItem?.ToString() ?? "Equal";
            var amounts = method == "Custom"
                ? ParseAmounts(AmountsEditor.Text, people.Count)
                : SplitAllocationCalculator.CalculateEqualShares(transaction.Amount, people.Count).ToList();

            var participants = people.Select((person, index) => new SplitParticipant
            {
                Name = person.Name,
                Contact = person.Contact,
                AmountOwed = amounts[index]
            }).ToList();

            await _splitService.CreateSplitAsync(new ExpenseSplit
            {
                TransactionSyncId = transaction.SyncId,
                Title = string.IsNullOrWhiteSpace(TitleEntry.Text)
                    ? transaction.Note
                    : TitleEntry.Text.Trim(),
                TotalAmount = transaction.Amount,
                Currency = "CAD",
                SplitMethod = method
            }, participants);

            SplitCreated?.Invoke(this, EventArgs.Empty);
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Cannot Create Split", ex.Message, "OK");
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e) =>
        await Navigation.PopModalAsync();

    private static List<(string Name, string Contact)> ParsePeople(string? value)
    {
        var people = (value ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('|', 2, StringSplitOptions.TrimEntries))
            .Where(parts => !string.IsNullOrWhiteSpace(parts[0]))
            .Select(parts => (
                Name: parts[0].Trim(),
                Contact: parts.Length > 1 ? parts[1].Trim() : string.Empty))
            .ToList();

        if (people.Count == 0)
            throw new InvalidOperationException("Add at least one person, one per line.");
        if (people.Select(person => person.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != people.Count)
            throw new InvalidOperationException("Each person must have a unique name.");

        return people;
    }

    private static List<decimal> ParseAmounts(string? value, int participantCount)
    {
        var lines = (value ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length != participantCount)
            throw new InvalidOperationException("Enter one custom amount for each person.");

        var amounts = new List<decimal>();
        foreach (var line in lines)
        {
            var normalized = line.Trim().TrimStart('$');
            if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
                && !decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                throw new InvalidOperationException($"'{line.Trim()}' is not a valid amount.");
            }

            amounts.Add(amount);
        }

        return amounts;
    }

}
