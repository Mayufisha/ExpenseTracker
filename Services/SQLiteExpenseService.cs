using ExpenseTracker.Models;
using SQLite;

namespace ExpenseTracker.Services;

public class SQLiteExpenseService : IExpenseService
{
    private readonly SQLiteAsyncConnection _db;
    private bool _initialized;

    public SQLiteExpenseService(string databasePath)
    {
        _db = new SQLiteAsyncConnection(databasePath);
    }

    private async Task InitAsync()
    {
        if (_initialized) return;

        await _db.CreateTableAsync<Category>();
        await _db.CreateTableAsync<Transaction>();
        await EnsureTransactionSchemaAsync();

        var count = await _db.Table<Category>().CountAsync();
        if (count == 0)
        {
            var defaults = new[]
            {
                new Category { Name = "Food",       ColorHex = "#FF9800" },
                new Category { Name = "Transport",  ColorHex = "#4CAF50" },
                new Category { Name = "Bills",      ColorHex = "#F44336" },
                new Category { Name = "Salary",     ColorHex = "#2196F3" },
                new Category { Name = "Investments", ColorHex = "#3F51B5" },
                new Category { Name = "Debt",       ColorHex = "#795548" },
                new Category { Name = "Other",      ColorHex = "#9E9E9E" }
            };
            await _db.InsertAllAsync(defaults);
        }

        _initialized = true;
    }

    private async Task EnsureTransactionSchemaAsync()
    {
        await TryAddColumnAsync("Type TEXT");
        await TryAddColumnAsync("FinancialAccountId INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync("InstitutionName TEXT");
        await TryAddColumnAsync("AccountName TEXT");
        await TryAddColumnAsync("StatementFileName TEXT");

        await _db.ExecuteAsync(
            "UPDATE \"Transaction\" SET Type = CASE WHEN IsIncome = 1 THEN 'Income' ELSE 'Expense' END WHERE Type IS NULL OR TRIM(Type) = ''");
    }

    private async Task TryAddColumnAsync(string columnDefinition)
    {
        try
        {
            await _db.ExecuteAsync($"ALTER TABLE \"Transaction\" ADD COLUMN {columnDefinition}");
        }
        catch
        {
            // Column already exists.
        }
    }

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync()
    {
        await InitAsync();
        return await _db.Table<Category>().OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<IReadOnlyList<Transaction>> GetTransactionsAsync()
    {
        await InitAsync();
        var categories = await _db.Table<Category>().ToListAsync();
        var txs = await _db.Table<Transaction>().OrderByDescending(t => t.Date).ToListAsync();

        foreach (var t in txs)
        {
            t.Category = categories.FirstOrDefault(c => c.Id == t.CategoryId);
            if (string.IsNullOrWhiteSpace(t.Type))
            {
                t.Type = t.IsIncome ? TransactionType.Income.ToString() : TransactionType.Expense.ToString();
            }
        }

        return txs;
    }

    public async Task AddOrUpdateTransactionAsync(Transaction transaction)
    {
        await InitAsync();

        if (string.IsNullOrWhiteSpace(transaction.Type))
        {
            transaction.Type = transaction.IsIncome
                ? TransactionType.Income.ToString()
                : TransactionType.Expense.ToString();
        }

        transaction.IsIncome = transaction.ParsedType == TransactionType.Income;

        if (transaction.Id == 0)
            await _db.InsertAsync(transaction);
        else
            await _db.UpdateAsync(transaction);
    }

    public async Task DeleteTransactionAsync(int id)
    {
        await InitAsync();
        await _db.DeleteAsync<Transaction>(id);
    }

    public async Task ClearAllTransactionsAsync()
    {
        await InitAsync();
        await _db.DeleteAllAsync<Transaction>();
    }

}
