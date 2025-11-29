using System.Collections.ObjectModel;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.ViewModels;

public class TransactionsViewModel : BaseViewModel
{
    private readonly IExpenseService _expenseService;

    public ObservableCollection<Transaction> Transactions { get; } = new();

    public TransactionsViewModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        Transactions.Clear();

        var items = await _expenseService.GetTransactionsAsync();
        foreach (var t in items)
            Transactions.Add(t);

        IsBusy = false;
    }
}
