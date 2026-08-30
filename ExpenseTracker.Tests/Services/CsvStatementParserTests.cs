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

    [Fact]
    public async Task ParseAsync_PositiveExpenseConvention_TreatsPositiveBankAmountsAsExpenses()
    {
        var path = await CreateCsvAsync(
            "Date,Description,Amount\n" +
            "2026-08-01,Groceries,82.40\n" +
            "2026-08-02,Payroll,-1000.00\n");

        try
        {
            var result = await CsvStatementParser.ParseAsync(
                path,
                StatementAmountConvention.PositiveAmountsAreExpenses);

            Assert.Equal(TransactionType.Expense, result[0].Type);
            Assert.Equal(TransactionType.Income, result[1].Type);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ParseAsync_DebitAndCreditColumns_IgnoreAmountConvention()
    {
        var path = await CreateCsvAsync(
            "Date,Description,Debit,Credit\n" +
            "2026-08-01,Groceries,82.40,\n" +
            "2026-08-02,Payroll,,1000.00\n");

        try
        {
            var result = await CsvStatementParser.ParseAsync(
                path,
                StatementAmountConvention.PositiveAmountsAreExpenses);

            Assert.Equal(TransactionType.Expense, result[0].Type);
            Assert.Equal(TransactionType.Income, result[1].Type);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ParseAsync_OutgoingETransfer_OverridesIncomeSign()
    {
        var path = await CreateCsvAsync(
            "Date,Description,Amount\n" +
            "2026-08-01,INTERAC E-TRANSFER SENT TO ALEX,75.00\n");

        try
        {
            var result = await CsvStatementParser.ParseAsync(
                path,
                StatementAmountConvention.NegativeAmountsAreExpenses);

            Assert.Equal(TransactionType.Expense, Assert.Single(result).Type);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ParseAsync_GenericETransfer_DefaultsToExpense()
    {
        var path = await CreateCsvAsync(
            "Date,Description,Amount\n" +
            "2026-08-15,INTERAC e-Transfer,85.00\n");

        try
        {
            var transactions = await CsvStatementParser.ParseAsync(
                path,
                StatementAmountConvention.NegativeAmountsAreExpenses);

            Assert.Single(transactions);
            Assert.Equal(TransactionType.Expense, transactions[0].Type);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ParseAsync_OutgoingETransfer_OverridesCreditColumn()
    {
        var path = await CreateCsvAsync(
            "Date,Description,Debit,Credit\n" +
            "2026-08-01,E-TRANSFER SENT TO MORGAN,,125.00\n");

        try
        {
            var result = await CsvStatementParser.ParseAsync(path, "Bank Account");

            Assert.Equal(TransactionType.Expense, Assert.Single(result).Type);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ParseAsync_IncomingETransfer_OverridesExpenseSign()
    {
        var path = await CreateCsvAsync(
            "Date,Description,Amount\n" +
            "2026-08-01,INTERAC E-TRANSFER RECEIVED FROM TAYLOR,75.00\n");

        try
        {
            var result = await CsvStatementParser.ParseAsync(
                path,
                StatementAmountConvention.PositiveAmountsAreExpenses);

            Assert.Equal(TransactionType.Income, Assert.Single(result).Type);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ParseAsync_TransactionTypeColumn_OverridesPositiveAmount()
    {
        var path = await CreateCsvAsync(
            "Date,Description,Amount,Transaction Type\n" +
            "2026-08-01,INTERAC E-TRF TO ALEX,75.00,Debit\n" +
            "2026-08-02,INTERAC E-TRF FROM MORGAN,125.00,Credit\n");

        try
        {
            var result = await CsvStatementParser.ParseAsync(
                path,
                StatementAmountConvention.NegativeAmountsAreExpenses);

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
