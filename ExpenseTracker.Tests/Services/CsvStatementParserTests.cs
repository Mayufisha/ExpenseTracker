using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Tests.Services;

public class CsvStatementParserTests
{
    [Fact]
    public async Task ParseAsync_BankStatement_UsesSignedAmountDirection()
    {
        var path = await CreateCsvAsync(
            "Date,Description,Amount\n" +
            "2026-08-01,Coffee,-5.25\n" +
            "2026-08-02,Salary,1000.00\n");

        try
        {
            var result = await CsvStatementParser.ParseAsync(path, "Bank Account");

            Assert.Equal(2, result.Count);
            Assert.Equal(TransactionType.Expense, result[0].Type);
            Assert.Equal(5.25m, result[0].Amount);
            Assert.Equal(TransactionType.Income, result[1].Type);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ParseAsync_CreditCardStatement_TreatsPositiveChargesAsExpenses()
    {
        var path = await CreateCsvAsync(
            "Transaction Date,Description,Amount\n" +
            "2026-08-01,Groceries,82.40\n" +
            "2026-08-02,Refund,-10.00\n");

        try
        {
            var result = await CsvStatementParser.ParseAsync(path, "Credit Card");

            Assert.Equal(TransactionType.Expense, result[0].Type);
            Assert.Equal(TransactionType.Income, result[1].Type);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<string> CreateCsvAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"statement-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
