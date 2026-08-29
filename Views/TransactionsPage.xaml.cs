using ExpenseTracker.Models;
using ExpenseTracker.Services;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class TransactionsPage : ContentPage
{
    private readonly TransactionsViewModel _viewModel;
    private readonly IExpenseService _expenseService;
    private readonly ISplitService _splitService;

    public TransactionsPage(
        TransactionsViewModel viewModel,
        IExpenseService expenseService,
        ISplitService splitService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _expenseService = expenseService;
        _splitService = splitService;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    async void OnAddClicked(object sender, EventArgs e)
    {
        var page = new AddEditTransactionPage(_expenseService);
        await Navigation.PushModalAsync(page);
    }

    async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection == null || e.CurrentSelection.Count == 0)
            return;

        var tx = e.CurrentSelection[0] as Transaction;
        ((CollectionView)sender).SelectedItem = null;

        if (tx == null) return;

        var page = new AddEditTransactionPage(_expenseService, tx);
        await Navigation.PushModalAsync(page);
    }

    async void OnDeleteSwipeInvoked(object sender, EventArgs e)
    {
        if (sender is not SwipeItem swipeItem)
            return;

        if (swipeItem.BindingContext is not Transaction tx)
            return;

        var linkedSplits = (await _splitService.GetSplitsAsync())
            .Where(split => split.TransactionSyncId == tx.SyncId)
            .ToList();
        var message = linkedSplits.Count == 0
            ? $"Delete transaction \"{tx.Note}\"?"
            : $"Delete transaction \"{tx.Note}\" and its linked split?";
        var confirm = await DisplayAlert(
            "Delete",
            message,
            "Yes", "No");

        if (!confirm) return;

        foreach (var split in linkedSplits)
            await _splitService.DeleteSplitAsync(split.SyncId);
        await _expenseService.DeleteTransactionAsync(tx.Id);
        await _viewModel.LoadAsync();
    }

    async void OnSplitSwipeInvoked(object sender, EventArgs e)
    {
        if (sender is not SwipeItem { BindingContext: Transaction transaction }) return;
        if (transaction.ParsedType != TransactionType.Expense)
        {
            await DisplayAlert("Cannot Split", "Only expense transactions can be split.", "OK");
            return;
        }

        var existing = (await _splitService.GetSplitsAsync())
            .Any(split => split.TransactionSyncId == transaction.SyncId);
        if (existing)
        {
            await DisplayAlert("Already Split", "This transaction already has a split.", "OK");
            return;
        }

        await Navigation.PushModalAsync(new NavigationPage(
            new CreateSplitPage(_expenseService, _splitService, transaction)));
    }

}
