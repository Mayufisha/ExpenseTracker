using ExpenseTracker.Models;
using ExpenseTracker.Tests.TestDoubles;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Tests.ViewModels;

public class DashboardViewModelTests
{
    [Fact]
    public async Task LoadAsync_ComputesIncomeExpenseAssetsAndLiabilities()
    {
        var today = DateTime.Today;
        var transactions = new[]
        {
            NewTransaction(1000m, TransactionType.Income, today),
            NewTransaction(300m, TransactionType.Expense, today),
            NewTransaction(5000m, TransactionType.Asset, today),
            NewTransaction(1200m, TransactionType.Liability, today)
        };

        var service = new FakeExpenseService(transactions);
        var vm = new DashboardViewModel(service);

        await vm.LoadAsync();

        Assert.Equal(1000m, vm.TotalIncome);
        Assert.Equal(300m, vm.TotalExpense);
        Assert.Equal(5000m, vm.TotalAssets);
        Assert.Equal(1200m, vm.TotalLiabilities);
        Assert.Equal(700m, vm.NetCashFlow);
        Assert.Equal(3800m, vm.NetWorth);
    }

    private static Transaction NewTransaction(decimal amount, TransactionType type, DateTime date)
    {
        var tx = new Transaction
        {
            Amount = amount,
            Date = date
        };
        tx.ParsedType = type;
        return tx;
    }
}
