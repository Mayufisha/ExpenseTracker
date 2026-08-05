using ExpenseTracker.Models;
using ExpenseTracker.Services;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class FinancialAccountsPage : ContentPage
{
    private readonly FinancialAccountsViewModel _viewModel;
    private readonly IStatementImportService _statementImportService;

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

    private async void OnAddClicked(object sender, EventArgs e)
    {
        var account = await PromptForAccountAsync(null);
        if (account != null)
            await _viewModel.SaveAccountAsync(account);
    }

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0) return;
        var account = e.CurrentSelection[0] as FinancialAccount;
        ((CollectionView)sender).SelectedItem = null;
        if (account == null) return;

        var edited = await PromptForAccountAsync(account);
        if (edited != null)
            await _viewModel.SaveAccountAsync(edited);
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
            await using var stream = await file.OpenReadAsync();
            var result = await _statementImportService.AttachAndImportAsync(account, stream, file.FileName);
            await DisplayAlert("Statement Attached", result.Message, "OK");
            await _viewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Statement Error", ex.Message, "OK");
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
            await _viewModel.DeleteAccountAsync(account);
    }

    private async Task<FinancialAccount?> PromptForAccountAsync(FinancialAccount? existing)
    {
        var institution = await DisplayPromptAsync(
            existing == null ? "Add Institution" : "Edit Institution",
            "Institution name:",
            initialValue: existing?.InstitutionName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(institution)) return null;

        var accountName = await DisplayPromptAsync(
            "Account",
            "Account name (e.g. Chequing, Visa):",
            initialValue: existing?.AccountName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(accountName)) return null;

        var accountType = await DisplayActionSheet(
            "Account Type",
            "Cancel",
            null,
            "Bank Account",
            "Credit Card");
        if (accountType == "Cancel" || string.IsNullOrWhiteSpace(accountType)) return null;

        var lastFour = await DisplayPromptAsync(
            "Account",
            "Last four digits (optional):",
            keyboard: Keyboard.Numeric,
            maxLength: 4,
            initialValue: existing?.LastFour ?? string.Empty);

        var account = existing ?? new FinancialAccount();
        account.InstitutionName = institution.Trim();
        account.AccountName = accountName.Trim();
        account.AccountType = accountType;
        account.LastFour = lastFour?.Trim() ?? string.Empty;
        return account;
    }
}
