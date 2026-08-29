using ExpenseTracker.Models;
using ExpenseTracker.Services;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class SplitsPage : ContentPage
{
    private readonly SplitsViewModel _viewModel;
    private readonly IExpenseService _expenseService;
    private readonly ISplitService _splitService;
    private readonly IPaymentRequestService _paymentRequestService;

    public SplitsPage(
        SplitsViewModel viewModel,
        IExpenseService expenseService,
        ISplitService splitService,
        IPaymentRequestService paymentRequestService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _expenseService = expenseService;
        _splitService = splitService;
        _paymentRequestService = paymentRequestService;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private Task OpenCreatePageAsync(Transaction? transaction = null)
    {
        var page = new CreateSplitPage(_expenseService, _splitService, transaction);
        page.SplitCreated += async (_, _) => await _viewModel.LoadAsync();
        return Navigation.PushModalAsync(new NavigationPage(page));
    }

    private async void OnNewSplitClicked(object sender, EventArgs e) =>
        await OpenCreatePageAsync();

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is CollectionView collection)
            collection.SelectedItem = null;
        if (e.CurrentSelection.FirstOrDefault() is not ExpenseSplit split) return;

        await Navigation.PushAsync(new SplitDetailsPage(
            split,
            _splitService,
            _paymentRequestService));
    }
}
