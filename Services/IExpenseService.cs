using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public interface IExpenseService
{
    Task<IReadOnlyList<Transaction>> GetTransactionsAsync();
    Task<IReadOnlyList<Category>> GetCategoriesAsync();

    Task AddOrUpdateTransactionAsync(Transaction transaction);
    Task DeleteTransactionAsync(int id);

    Task ClearAllTransactionsAsync();
}
