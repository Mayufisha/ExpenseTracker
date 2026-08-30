using ExpenseTracker.Models;
using SQLite;

namespace ExpenseTracker.Services;

public class SQLiteFinancialAccountService : IFinancialAccountService
{
    private readonly SQLiteAsyncConnection _db;
    private readonly IUserDataContext _userContext;
    private bool _initialized;

    public SQLiteFinancialAccountService(string databasePath, IUserDataContext userContext)
    {
        _db = new SQLiteAsyncConnection(databasePath);
        _userContext = userContext;
    }

    private async Task InitAsync()
    {
        if (_initialized) return;

        await _db.CreateTableAsync<FinancialAccount>();
        await _db.CreateTableAsync<StatementAttachment>();
        await SQLiteSchema.EnsureOwnerColumnAsync(_db, nameof(FinancialAccount));
        await SQLiteSchema.EnsureOwnerColumnAsync(_db, nameof(StatementAttachment));
        await SQLiteSchema.EnsureTextColumnAsync(_db, nameof(FinancialAccount), nameof(FinancialAccount.AmountConvention));
        await EnsureStatementSchemaAsync();
        _initialized = true;
    }

    private async Task EnsureStatementSchemaAsync()
    {
        try
        {
            await _db.ExecuteAsync(
                "ALTER TABLE StatementAttachment ADD COLUMN CloudStoragePath TEXT");
        }
        catch
        {
            // Column already exists.
        }
    }

    public async Task<IReadOnlyList<FinancialAccount>> GetAccountsAsync()
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        var accounts = await _db.Table<FinancialAccount>()
            .Where(a => a.OwnerUserId == ownerUserId)
            .OrderBy(a => a.InstitutionName)
            .ThenBy(a => a.AccountName)
            .ToListAsync();

        var statementCounts = (await _db.Table<StatementAttachment>()
                .Where(statement => statement.OwnerUserId == ownerUserId)
                .ToListAsync())
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
        var ownerUserId = _userContext.RequireCurrentUserId();
        if (account.Id != 0 && await _db.Table<FinancialAccount>()
                .Where(item => item.Id == account.Id && item.OwnerUserId == ownerUserId)
                .CountAsync() == 0)
            throw new InvalidOperationException("This financial account does not belong to the signed-in user.");
        account.OwnerUserId = ownerUserId;

        if (account.Id == 0)
            await _db.InsertAsync(account);
        else
            await _db.UpdateAsync(account);
    }

    public async Task DeleteAccountAsync(int accountId)
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        var statements = await _db.Table<StatementAttachment>()
            .Where(s => s.FinancialAccountId == accountId && s.OwnerUserId == ownerUserId)
            .ToListAsync();

        foreach (var statement in statements)
        {
            if (!string.IsNullOrWhiteSpace(statement.StoredFilePath) && File.Exists(statement.StoredFilePath))
            {
                File.Delete(statement.StoredFilePath);
            }

            await _db.DeleteAsync(statement);
        }

        await _db.ExecuteAsync(
            "DELETE FROM FinancialAccount WHERE Id = ? AND OwnerUserId = ?",
            accountId,
            ownerUserId);
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
        var ownerUserId = _userContext.RequireCurrentUserId();
        return await _db.Table<StatementAttachment>()
            .Where(s => s.FinancialAccountId == accountId && s.OwnerUserId == ownerUserId)
            .OrderByDescending(s => s.AttachedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<StatementAttachment>> GetAllStatementsAsync()
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        return await _db.Table<StatementAttachment>()
            .Where(s => s.OwnerUserId == ownerUserId)
            .OrderByDescending(s => s.AttachedAt)
            .ToListAsync();
    }

    public async Task<bool> HasStatementAsync(int accountId, string fileHash)
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        return await _db.Table<StatementAttachment>()
            .Where(s => s.FinancialAccountId == accountId
                && s.FileHash == fileHash
                && s.OwnerUserId == ownerUserId)
            .CountAsync() > 0;
    }

    public async Task AddStatementAsync(StatementAttachment statement)
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        if (await _db.Table<FinancialAccount>()
                .Where(account => account.Id == statement.FinancialAccountId
                    && account.OwnerUserId == ownerUserId)
                .CountAsync() == 0)
            throw new InvalidOperationException("The statement account does not belong to the signed-in user.");
        if (statement.Id != 0 && await _db.Table<StatementAttachment>()
                .Where(item => item.Id == statement.Id && item.OwnerUserId == ownerUserId)
                .CountAsync() == 0)
            throw new InvalidOperationException("This statement does not belong to the signed-in user.");
        statement.OwnerUserId = ownerUserId;
        if (statement.Id == 0)
            await _db.InsertAsync(statement);
        else
            await _db.UpdateAsync(statement);
    }

    public Task CloseAsync() => _db.CloseAsync();
}
