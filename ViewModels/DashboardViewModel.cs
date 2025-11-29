using System.Collections.ObjectModel;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private readonly IExpenseService _expenseService;

    public ObservableCollection<Transaction> Transactions { get; } = new();

    decimal totalIncome;
    public decimal TotalIncome
    {
        get => totalIncome;
        set { totalIncome = value; OnPropertyChanged(); }
    }

    decimal totalExpense;
    public decimal TotalExpense
    {
        get => totalExpense;
        set { totalExpense = value; OnPropertyChanged(); }
    }

    decimal balance;
    public decimal Balance
    {
        get => balance;
        set { balance = value; OnPropertyChanged(); }
    }

    public DashboardViewModel(IExpenseService expenseService)
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

        TotalIncome = Transactions.Where(t => t.IsIncome).Sum(t => t.Amount);
        TotalExpense = Transactions.Where(t => !t.IsIncome).Sum(t => t.Amount);
        Balance = TotalIncome - TotalExpense;

        IsBusy = false;
    }
}
