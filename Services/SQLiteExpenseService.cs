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

        var count = await _db.Table<Category>().CountAsync();
        if (count == 0)
        {
            var defaults = new[]
            {
                new Category { Name = "Food",       ColorHex = "#FF9800" },
                new Category { Name = "Transport",  ColorHex = "#4CAF50" },
                new Category { Name = "Bills",      ColorHex = "#F44336" },
                new Category { Name = "Salary",     ColorHex = "#2196F3" },
                new Category { Name = "Other",      ColorHex = "#9E9E9E" }
            };
            await _db.InsertAllAsync(defaults);
        }

        _initialized = true;
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
        }

        return txs;
    }

    public async Task AddOrUpdateTransactionAsync(Transaction transaction)
    {
        await InitAsync();

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
