using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public interface IFinancialAccountService
{
    Task<IReadOnlyList<FinancialAccount>> GetAccountsAsync();
    Task AddOrUpdateAccountAsync(FinancialAccount account);
    Task DeleteAccountAsync(int accountId);
    Task ClearAllAsync();
    Task<IReadOnlyList<StatementAttachment>> GetStatementsAsync(int accountId);
    Task<IReadOnlyList<StatementAttachment>> GetAllStatementsAsync();
    Task<bool> HasStatementAsync(int accountId, string fileHash);
    Task AddStatementAsync(StatementAttachment statement);
}
