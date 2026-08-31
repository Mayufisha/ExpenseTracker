using ExpenseTracker.Models;
using ExpenseTracker.Services;
using ExpenseTracker.ViewModels;
using System.Globalization;

namespace ExpenseTracker.Views;

public partial class FinancialAccountsPage : ContentPage
{
    private readonly FinancialAccountsViewModel _viewModel;
    private readonly IStatementImportService _statementImportService;
    private FinancialAccount? _editingAccount;
    private bool _settingEditorValues;

    public FinancialAccountsPage(
        FinancialAccountsViewModel viewModel,
        IStatementImportService statementImportService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _statementImportService = statementImportService;
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
        var account = e.CurrentSelection[0] as FinancialAccount;
        ((CollectionView)sender).SelectedItem = null;
        if (account != null) ShowEditor(account);
    }

    private async void OnSaveAccountClicked(object sender, EventArgs e)
    {
        var institution = InstitutionEntry.Text?.Trim() ?? string.Empty;
        var accountName = AccountNameEntry.Text?.Trim() ?? string.Empty;
        var lastFour = LastFourEntry.Text?.Trim() ?? string.Empty;
        var balanceText = CurrentBalanceEntry.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(institution) || string.IsNullOrWhiteSpace(accountName))
        {
            ShowEditorError("Institution and account name are required.");
            return;
        }

        if (AccountTypePicker.SelectedIndex < 0 || AmountConventionPicker.SelectedIndex < 0)
        {
            ShowEditorError("Choose an account type and statement amount rule.");
            return;
        }

        if (lastFour.Length is > 0 and not 4 || lastFour.Any(character => !char.IsDigit(character)))
        {
            ShowEditorError("Last four must be exactly four digits or left empty.");
            return;
        }

        decimal? currentBalance = null;
        if (!string.IsNullOrWhiteSpace(balanceText))
        {
            var styles = NumberStyles.Number | NumberStyles.AllowCurrencySymbol;
            if (!decimal.TryParse(balanceText, styles, CultureInfo.CurrentCulture, out var parsedBalance)
                && !decimal.TryParse(balanceText, styles, CultureInfo.InvariantCulture, out parsedBalance))
            {
                ShowEditorError("Enter a valid current balance or leave it blank.");
                return;
            }

            if (Math.Abs(parsedBalance) > 1_000_000_000_000m)
            {
                ShowEditorError("The current balance is outside the supported range.");
                return;
            }

            currentBalance = parsedBalance;
        }

        try
        {
            var account = _editingAccount ?? new FinancialAccount();
            account.InstitutionName = institution;
            account.AccountName = accountName;
            account.AccountType = AccountTypePicker.SelectedItem?.ToString() ?? "Bank Account";
            account.LastFour = lastFour;
            account.CurrentBalance = currentBalance;
            account.BalanceAsOf = currentBalance.HasValue ? BalanceAsOfPicker.Date : null;
            account.ParsedAmountConvention = AmountConventionPicker.SelectedIndex == 1
                ? StatementAmountConvention.PositiveAmountsAreExpenses
                : StatementAmountConvention.NegativeAmountsAreExpenses;

            await _viewModel.SaveAccountAsync(account);
            PageStatusLabel.Text = $"Saved {account.DisplayName}.";
            HideEditor();
        }
        catch (Exception exception)
        {
            ShowEditorError(exception.Message);
        }
    }

    private void OnCancelAccountClicked(object sender, EventArgs e) => HideEditor();

    private void OnAccountTypeChanged(object sender, EventArgs e)
    {
        if (_settingEditorValues || _editingAccount != null) return;
        AmountConventionPicker.SelectedIndex = AccountTypePicker.SelectedIndex == 1 ? 1 : 0;
    }

    private async void OnAttachStatementClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: FinancialAccount account }) return;

        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Choose bank or credit card statement",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".csv", ".pdf" } },
                { DevicePlatform.Android, new[] { "text/csv", "text/comma-separated-values", "application/pdf" } },
                { DevicePlatform.iOS, new[] { "public.comma-separated-values-text", "com.adobe.pdf" } },
                { DevicePlatform.MacCatalyst, new[] { "public.comma-separated-values-text", "com.adobe.pdf" } }
            })
        });

        if (file == null) return;

        try
        {
            PageStatusLabel.Text = $"Importing {file.FileName}...";
            await using var stream = await file.OpenReadAsync();
            var result = await _statementImportService.AttachAndImportAsync(account, stream, file.FileName);
            PageStatusLabel.Text = result.Message;
            await _viewModel.LoadAsync();
        }
        catch (Exception exception)
        {
            PageStatusLabel.Text = $"Statement error: {exception.Message}";
        }
    }

    private async void OnDeleteSwipeInvoked(object sender, EventArgs e)
    {
        if (sender is not SwipeItem { BindingContext: FinancialAccount account }) return;

        var confirm = await DisplayAlert(
            "Delete Account",
            $"Delete {account.DisplayName} and its attached statement files? Imported transactions will remain.",
            "Delete",
            "Cancel");

        if (confirm)
        {
            await _viewModel.DeleteAccountAsync(account);
            PageStatusLabel.Text = $"Deleted {account.DisplayName}.";
            if (_editingAccount?.Id == account.Id) HideEditor();
        }
    }

    private void ShowEditor(FinancialAccount? account)
    {
        _editingAccount = account;
        _settingEditorValues = true;
        EditorTitleLabel.Text = account == null ? "Add financial account" : "Edit financial account";
        InstitutionEntry.Text = account?.InstitutionName ?? string.Empty;
        AccountNameEntry.Text = account?.AccountName ?? string.Empty;
        var accountTypeIndex = account == null ? 0 : AccountTypePicker.Items.IndexOf(account.AccountType);
        AccountTypePicker.SelectedIndex = accountTypeIndex >= 0 ? accountTypeIndex : 0;
        LastFourEntry.Text = account?.LastFour ?? string.Empty;
        CurrentBalanceEntry.Text = account?.CurrentBalance?.ToString("0.00", CultureInfo.CurrentCulture) ?? string.Empty;
        BalanceAsOfPicker.Date = account?.BalanceAsOf?.Date ?? DateTime.Today;
        AmountConventionPicker.SelectedIndex = account?.ParsedAmountConvention == StatementAmountConvention.PositiveAmountsAreExpenses
            ? 1
            : 0;
        EditorErrorLabel.IsVisible = false;
        AccountEditor.IsVisible = true;
        _settingEditorValues = false;
        InstitutionEntry.Focus();
    }

    private void HideEditor()
    {
        _editingAccount = null;
        AccountEditor.IsVisible = false;
        EditorErrorLabel.IsVisible = false;
    }

    private void ShowEditorError(string message)
    {
        EditorErrorLabel.Text = message;
        EditorErrorLabel.IsVisible = true;
    }
}
