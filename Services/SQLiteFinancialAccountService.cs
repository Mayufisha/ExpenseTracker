using ExpenseTracker.Models;
using SQLite;

namespace ExpenseTracker.Services;

public class SQLiteFinancialAccountService : IFinancialAccountService
{
    private readonly SQLiteAsyncConnection _db;
    private bool _initialized;

    public SQLiteFinancialAccountService(string databasePath)
    {
        _db = new SQLiteAsyncConnection(databasePath);
    }

    private async Task InitAsync()
    {
        if (_initialized) return;

        await _db.CreateTableAsync<FinancialAccount>();
        await _db.CreateTableAsync<StatementAttachment>();
        _initialized = true;
    }

    public async Task<IReadOnlyList<FinancialAccount>> GetAccountsAsync()
    {
        await InitAsync();
        var accounts = await _db.Table<FinancialAccount>()
            .OrderBy(a => a.InstitutionName)
            .ThenBy(a => a.AccountName)
            .ToListAsync();

        var statementCounts = (await _db.Table<StatementAttachment>().ToListAsync())
            .GroupBy(s => s.FinancialAccountId)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var account in accounts)
        {
            account.StatementCount = statementCounts.GetValueOrDefault(account.Id);
        }

        return accounts;
    }

    public async Task AddOrUpdateAccountAsync(FinancialAccount account)
    {
        await InitAsync();

        if (account.Id == 0)
            await _db.InsertAsync(account);
        else
            await _db.UpdateAsync(account);
    }

    public async Task DeleteAccountAsync(int accountId)
    {
        await InitAsync();
        var statements = await _db.Table<StatementAttachment>()
            .Where(s => s.FinancialAccountId == accountId)
            .ToListAsync();

        foreach (var statement in statements)
        {
            if (!string.IsNullOrWhiteSpace(statement.StoredFilePath) && File.Exists(statement.StoredFilePath))
            {
                File.Delete(statement.StoredFilePath);
            }

            await _db.DeleteAsync(statement);
        }

        await _db.DeleteAsync<FinancialAccount>(accountId);
    }

    public async Task ClearAllAsync()
    {
        await InitAsync();
        var accounts = await GetAccountsAsync();
        foreach (var account in accounts)
        {
            await DeleteAccountAsync(account.Id);
        }
    }

    public async Task<IReadOnlyList<StatementAttachment>> GetStatementsAsync(int accountId)
    {
        await InitAsync();
        return await _db.Table<StatementAttachment>()
            .Where(s => s.FinancialAccountId == accountId)
            .OrderByDescending(s => s.AttachedAt)
            .ToListAsync();
    }

    public async Task<bool> HasStatementAsync(int accountId, string fileHash)
    {
        await InitAsync();
        return await _db.Table<StatementAttachment>()
            .Where(s => s.FinancialAccountId == accountId && s.FileHash == fileHash)
            .CountAsync() > 0;
    }

    public async Task AddStatementAsync(StatementAttachment statement)
    {
        await InitAsync();
        await _db.InsertAsync(statement);
    }
}
