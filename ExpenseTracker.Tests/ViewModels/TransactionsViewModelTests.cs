using ExpenseTracker.Models;
using ExpenseTracker.Tests.TestDoubles;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Tests.ViewModels;

public class TransactionsViewModelTests
{
    [Fact]
    public async Task SelectedRange_LastMonth_FiltersCorrectly()
    {
        var today = DateTime.Today;
        var lastMonth = today.AddMonths(-1);
        var lastMonthStart = new DateTime(lastMonth.Year, lastMonth.Month, 1);
        var twoMonthsAgo = today.AddMonths(-2);

        var service = new FakeExpenseService(new[]
        {
            NewExpense(lastMonthStart.AddDays(4)),
            NewExpense(lastMonthStart.AddDays(10)),
            NewExpense(today),
            NewExpense(twoMonthsAgo)
        });

        var vm = new TransactionsViewModel(service);
        await vm.LoadAsync();

        vm.SelectedRange = TimeRange.LastMonth;

        Assert.Equal(2, vm.Transactions.Count);
        Assert.All(vm.Transactions, t =>
            Assert.True(t.Date >= lastMonthStart && t.Date < lastMonthStart.AddMonths(1)));
    }

    [Fact]
    public async Task SelectedRange_LastThreeMonths_ExcludesOlderItems()
    {
        var today = DateTime.Today;
        var boundary = today.AddMonths(-3);

        var service = new FakeExpenseService(new[]
        {
            NewExpense(today),
            NewExpense(boundary),
            NewExpense(boundary.AddDays(-1))
        });

        var vm = new TransactionsViewModel(service);
        await vm.LoadAsync();

        vm.SelectedRange = TimeRange.LastThreeMonths;

        Assert.Equal(2, vm.Transactions.Count);
    }

    private static Transaction NewExpense(DateTime date)
    {
        var tx = new Transaction
        {
            Amount = 10m,
            Date = date
        };
        tx.ParsedType = TransactionType.Expense;
        return tx;
    }
}
