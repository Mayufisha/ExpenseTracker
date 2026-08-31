using ExpenseTracker.Models;
using ExpenseTracker.Services;
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
            NewTransaction(1200m, TransactionType.Liability, today),
            NewTransaction(40_000m, TransactionType.Expense, today.AddMonths(-1))
        };

        var service = new FakeExpenseService(transactions);
        var accountService = new FakeFinancialAccountService(
        [
            new FinancialAccount { AccountType = "Bank Account", CurrentBalance = 2500m },
            new FinancialAccount { AccountType = "Credit Card", CurrentBalance = 400m }
        ]);
        var vm = new DashboardViewModel(service, accountService);

        await vm.LoadAsync();

        Assert.Equal(1000m, vm.TotalIncome);
        Assert.Equal(300m, vm.TotalExpense);
        Assert.Equal(7500m, vm.TotalAssets);
        Assert.Equal(1600m, vm.TotalLiabilities);
        Assert.Equal(700m, vm.NetCashFlow);
        Assert.Equal(5900m, vm.NetWorth);
        Assert.Contains("2 account balances", vm.NetWorthStatus);
        Assert.Equal(4, vm.Transactions.Count);
        Assert.Equal(6, vm.MonthlyNetPoints.Count);

        vm.SelectedTrendMonths = 3;
        Assert.Equal(3, vm.MonthlyNetPoints.Count);

        vm.SelectedTrendMonths = 12;
        Assert.Equal(12, vm.MonthlyNetPoints.Count);
        Assert.All(vm.MonthlyNetPoints, point => Assert.Contains(' ', point.Label));

        vm.SelectedMonthFilter = vm.MonthFilters.First(option =>
            option.Key == today.AddMonths(-1).ToString("yyyy-MM"));

        Assert.Equal(40_000m, vm.TotalExpense);
        Assert.Equal(-40_000m, vm.NetCashFlow);
        Assert.Single(vm.Transactions);
        Assert.Equal(5900m, vm.NetWorth);
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

    private sealed class FakeFinancialAccountService(IEnumerable<FinancialAccount> accounts)
        : IFinancialAccountService
    {
        private readonly IReadOnlyList<FinancialAccount> _accounts = accounts.ToList();

        public Task<IReadOnlyList<FinancialAccount>> GetAccountsAsync() => Task.FromResult(_accounts);
        public Task AddOrUpdateAccountAsync(FinancialAccount account) => Task.CompletedTask;
        public Task DeleteAccountAsync(int accountId) => Task.CompletedTask;
        public Task ClearAllAsync() => Task.CompletedTask;
        public Task<IReadOnlyList<StatementAttachment>> GetStatementsAsync(int accountId) =>
            Task.FromResult<IReadOnlyList<StatementAttachment>>(Array.Empty<StatementAttachment>());
        public Task<IReadOnlyList<StatementAttachment>> GetAllStatementsAsync() =>
            Task.FromResult<IReadOnlyList<StatementAttachment>>(Array.Empty<StatementAttachment>());
        public Task<bool> HasStatementAsync(int accountId, string fileHash) => Task.FromResult(false);
        public Task AddStatementAsync(StatementAttachment statement) => Task.CompletedTask;
    }
}
