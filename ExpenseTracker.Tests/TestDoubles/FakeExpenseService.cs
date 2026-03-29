using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Tests.TestDoubles;

internal sealed class FakeExpenseService : IExpenseService
{
    private readonly List<Transaction> _transactions;
    private readonly List<Category> _categories;

    public FakeExpenseService(IEnumerable<Transaction> transactions, IEnumerable<Category>? categories = null)
    {
        _transactions = transactions.ToList();
        _categories = categories?.ToList() ?? new List<Category>
        {
            new() { Id = 1, Name = "Other" }
        };
    }

    public Task<IReadOnlyList<Transaction>> GetTransactionsAsync()
    {
        return Task.FromResult<IReadOnlyList<Transaction>>(_transactions
            .OrderByDescending(t => t.Date)
            .ToList());
    }

    public Task<IReadOnlyList<Category>> GetCategoriesAsync()
    {
        return Task.FromResult<IReadOnlyList<Category>>(_categories.ToList());
    }

    public Task AddOrUpdateTransactionAsync(Transaction transaction)
    {
        if (transaction.Id == 0)
        {
            transaction.Id = _transactions.Count == 0 ? 1 : _transactions.Max(t => t.Id) + 1;
            _transactions.Add(transaction);
            return Task.CompletedTask;
        }

        var index = _transactions.FindIndex(t => t.Id == transaction.Id);
        if (index >= 0)
        {
            _transactions[index] = transaction;
        }

        return Task.CompletedTask;
    }

    public Task DeleteTransactionAsync(int id)
    {
        _transactions.RemoveAll(t => t.Id == id);
        return Task.CompletedTask;
    }

    public Task ClearAllTransactionsAsync()
    {
        _transactions.Clear();
        return Task.CompletedTask;
    }
}
