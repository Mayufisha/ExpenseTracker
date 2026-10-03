using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Tests.Services;

public class SQLiteUserIsolationTests
{
    static SQLiteUserIsolationTests()
    {
    }

    [Fact]
    public async Task ExistingUnownedRows_AreMigratedButNotAssignedToNewUser()
    {
        var path = Path.Combine(Path.GetTempPath(), $"money-manager-legacy-{Guid.NewGuid():N}.db3");
        var legacyDatabase = new SQLite.SQLiteAsyncConnection(path);
        await legacyDatabase.ExecuteAsync(
            "CREATE TABLE \"Transaction\" (Id INTEGER PRIMARY KEY AUTOINCREMENT, SyncId TEXT, Amount TEXT NOT NULL, IsIncome INTEGER NOT NULL, Type TEXT, CategoryId INTEGER NOT NULL, Date TEXT NOT NULL, Note TEXT, FinancialAccountId INTEGER NOT NULL DEFAULT 0, InstitutionName TEXT, AccountName TEXT, StatementFileName TEXT)");
        await legacyDatabase.ExecuteAsync(
            "INSERT INTO \"Transaction\" (SyncId, Amount, IsIncome, Type, CategoryId, Date, Note) VALUES (?, ?, ?, ?, ?, ?, ?)",
            "legacy-row",
            "99.00",
            0,
            "Expense",
            1,
            DateTime.Today,
            "Legacy unowned data");
        await legacyDatabase.CloseAsync();

        var userContext = new UserDataContext();
        userContext.SetCurrentUser("new-user");
        var expenses = new SQLiteExpenseService(path, userContext);

        try
        {
            Assert.Empty(await expenses.GetTransactionsAsync());
        }
        finally
        {
            await expenses.CloseAsync();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetTransactionsAsync_CorrectsExistingOutgoingETransfer()
    {
        var path = Path.Combine(Path.GetTempPath(), $"money-manager-transfer-{Guid.NewGuid():N}.db3");
        var userContext = new UserDataContext();
        userContext.SetCurrentUser("transfer-user");
        var expenses = new SQLiteExpenseService(path, userContext);

        try
        {
            var category = (await expenses.GetCategoriesAsync()).First();
            var transaction = new Transaction
            {
                Amount = 85m,
                CategoryId = category.Id,
                Date = DateTime.Today,
                Note = "INTERAC E-TRANSFER SENT TO ALEX"
            };
            transaction.ParsedType = TransactionType.Income;
            await expenses.AddOrUpdateTransactionAsync(transaction);

            var corrected = Assert.Single(await expenses.GetTransactionsAsync());

            Assert.Equal(TransactionType.Expense, corrected.ParsedType);
        }
        finally
        {
            await expenses.CloseAsync();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Services_OnlyReadAndClearRowsOwnedByCurrentUser()
    {
        var path = Path.Combine(Path.GetTempPath(), $"money-manager-users-{Guid.NewGuid():N}.db3");
        var userContext = new UserDataContext();
        var expenses = new SQLiteExpenseService(path, userContext);
        var goals = new SQLiteGoalService(path, userContext);
        var schedule = new SQLiteScheduleService(path, userContext);
        var accounts = new SQLiteFinancialAccountService(path, userContext);
        var splits = new SQLiteSplitService(path, userContext);

        try
        {
            userContext.SetCurrentUser("user-a");
            var category = (await expenses.GetCategoriesAsync()).First();
            var transaction = new Transaction
            {
                Amount = 25m,
                CategoryId = category.Id,
                Date = DateTime.Today,
                Note = "User A purchase"
            };
            transaction.ParsedType = TransactionType.Expense;
            await expenses.AddOrUpdateTransactionAsync(transaction);
            await goals.AddOrUpdateGoalAsync(new Goal { Name = "User A goal", TargetAmount = 100m });
            await schedule.AddOrUpdateAsync(new ScheduledTransaction
            {
                Amount = 10m,
                ScheduledDate = DateTime.Today,
                Note = "User A schedule"
            });
            await accounts.AddOrUpdateAccountAsync(new FinancialAccount
            {
                InstitutionName = "User A Bank",
                AccountName = "Chequing"
            });
            await splits.CreateSplitAsync(
                new ExpenseSplit
                {
                    TransactionSyncId = transaction.SyncId,
                    Title = "User A split",
                    TotalAmount = 25m
                },
                [new SplitParticipant { Name = "Friend", AmountOwed = 10m }]);

            userContext.SetCurrentUser("user-b");
            Assert.Empty(await expenses.GetTransactionsAsync());
            Assert.Empty(await goals.GetGoalsAsync());
            Assert.Empty(await schedule.GetScheduledAsync());
            Assert.Empty(await accounts.GetAccountsAsync());
            Assert.Empty(await splits.GetSplitsAsync());

            var userBTransaction = new Transaction
            {
                Amount = 50m,
                CategoryId = category.Id,
                Date = DateTime.Today,
                Note = "User B purchase"
            };
            userBTransaction.ParsedType = TransactionType.Expense;
            await expenses.AddOrUpdateTransactionAsync(userBTransaction);

            userContext.SetCurrentUser("user-a");
            Assert.Single(await expenses.GetTransactionsAsync());
            await expenses.ClearAllTransactionsAsync();

            userContext.SetCurrentUser("user-b");
            var remaining = Assert.Single(await expenses.GetTransactionsAsync());
            Assert.Equal("User B purchase", remaining.Note);
        }
        finally
        {
            await splits.CloseAsync();
            await accounts.CloseAsync();
            await schedule.CloseAsync();
            await goals.CloseAsync();
            await expenses.CloseAsync();
            File.Delete(path);
        }
    }
}
