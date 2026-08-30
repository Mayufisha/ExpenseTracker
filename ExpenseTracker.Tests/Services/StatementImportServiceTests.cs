using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Tests.Services;

public class StatementImportServiceTests
{
    [Fact]
    public async Task ReclassifyAttachedTransactionsAsync_RepairsRowsUsingDetectedConvention()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"statement-repair-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "expenses.db3");
        var statementPath = Path.Combine(testDirectory, "statement.csv");
        await File.WriteAllTextAsync(
            statementPath,
            "Description,Type,Date,Amount\n" +
            "eTransfer to Recipient,TRANSFER,08/28/2026,-120.00\n" +
            "eTransfer Autodeposit,TRANSFER,08/23/2026,408.00\n" +
            "Employer,DEPOSIT,08/20/2026,1229.13\n" +
            "Credit Card,TRANSFER,07/28/2026,-125.75\n");

        var userContext = new UserDataContext();
        userContext.SetCurrentUser("repair-user");
        var expenses = new SQLiteExpenseService(databasePath, userContext);
        var accounts = new SQLiteFinancialAccountService(databasePath, userContext);

        try
        {
            var account = new FinancialAccount
            {
                InstitutionName = "Test Bank",
                AccountName = "Chequing",
                AccountType = "Bank Account"
            };
            account.ParsedAmountConvention = StatementAmountConvention.PositiveAmountsAreExpenses;
            await accounts.AddOrUpdateAccountAsync(account);

            var category = (await expenses.GetCategoriesAsync()).First();
            await AddImportedTransactionAsync(
                expenses,
                category.Id,
                account.Id,
                new DateTime(2026, 8, 20),
                1229.13m,
                "Employer",
                TransactionType.Expense);
            await AddImportedTransactionAsync(
                expenses,
                category.Id,
                account.Id,
                new DateTime(2026, 7, 28),
                125.75m,
                "Credit Card",
                TransactionType.Income);

            await accounts.AddStatementAsync(new StatementAttachment
            {
                FinancialAccountId = account.Id,
                OriginalFileName = "statement.csv",
                StoredFilePath = statementPath,
                FileType = "CSV",
                FileHash = "test-hash",
                ImportedTransactionCount = 4
            });

            var service = new StatementImportService(
                accounts,
                expenses,
                new NoOpCloudStatementSyncService(),
                userContext,
                testDirectory);

            var correctedCount = await service.ReclassifyAttachedTransactionsAsync();
            var repeatedCount = await service.ReclassifyAttachedTransactionsAsync();
            var corrected = await expenses.GetTransactionsAsync();

            Assert.Equal(2, correctedCount);
            Assert.Equal(0, repeatedCount);
            Assert.Equal(
                TransactionType.Income,
                corrected.Single(transaction => transaction.Note == "Employer").ParsedType);
            Assert.Equal(
                TransactionType.Expense,
                corrected.Single(transaction => transaction.Note == "Credit Card").ParsedType);
        }
        finally
        {
            await expenses.CloseAsync();
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private static async Task AddImportedTransactionAsync(
        IExpenseService expenses,
        int categoryId,
        int accountId,
        DateTime date,
        decimal amount,
        string description,
        TransactionType type)
    {
        var transaction = new Transaction
        {
            CategoryId = categoryId,
            FinancialAccountId = accountId,
            InstitutionName = "Test Bank",
            AccountName = "Chequing",
            StatementFileName = "statement.csv",
            Date = date,
            Amount = amount,
            Note = description
        };
        transaction.ParsedType = type;
        await expenses.AddOrUpdateTransactionAsync(transaction);
    }

    private sealed class NoOpCloudStatementSyncService : ICloudStatementSyncService
    {
        public Task<int> SyncPendingAsync() => Task.FromResult(0);
    }
}
