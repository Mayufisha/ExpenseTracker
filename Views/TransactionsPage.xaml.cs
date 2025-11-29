using ExpenseTracker.Models;
using ExpenseTracker.Services;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class TransactionsPage : ContentPage
{
    private readonly TransactionsViewModel _viewModel;
    private readonly IExpenseService _expenseService;

    public TransactionsPage(TransactionsViewModel viewModel, IExpenseService expenseService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _expenseService = expenseService;
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

        var confirm = await DisplayAlert(
            "Delete",
            $"Delete transaction \"{tx.Note}\"?",
            "Yes", "No");

        if (!confirm) return;

        await _expenseService.DeleteTransactionAsync(tx.Id);
        await _viewModel.LoadAsync();
    }
}
