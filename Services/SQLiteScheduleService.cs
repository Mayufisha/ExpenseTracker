using ExpenseTracker.Models;
using SQLite;

namespace ExpenseTracker.Services;

public class SQLiteScheduleService : IScheduleService
{
    private readonly SQLiteAsyncConnection _db;
    private readonly IUserDataContext _userContext;
    private bool _initialized;

    public SQLiteScheduleService(string databasePath, IUserDataContext userContext)
    {
        _db = new SQLiteAsyncConnection(databasePath);
        _userContext = userContext;
    }

    private async Task InitAsync()
    {
        if (_initialized) return;

        await _db.CreateTableAsync<ScheduledTransaction>();
        await SQLiteSchema.EnsureOwnerColumnAsync(_db, nameof(ScheduledTransaction));

        _initialized = true;
    }

    public async Task<IReadOnlyList<ScheduledTransaction>> GetScheduledAsync()
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        return await _db.Table<ScheduledTransaction>()
                        .Where(item => item.OwnerUserId == ownerUserId)
                        .OrderBy(s => s.ScheduledDate)
                        .ToListAsync();
    }

    public async Task AddOrUpdateAsync(ScheduledTransaction scheduled)
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        if (scheduled.Id != 0 && await _db.Table<ScheduledTransaction>()
                .Where(item => item.Id == scheduled.Id && item.OwnerUserId == ownerUserId)
                .CountAsync() == 0)
            throw new InvalidOperationException("This scheduled item does not belong to the signed-in user.");
        scheduled.OwnerUserId = ownerUserId;

        if (scheduled.Id == 0)
            await _db.InsertAsync(scheduled);
        else
            await _db.UpdateAsync(scheduled);
    }

    public async Task DeleteAsync(int id)
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        await _db.ExecuteAsync(
            "DELETE FROM ScheduledTransaction WHERE Id = ? AND OwnerUserId = ?",
            id,
            ownerUserId);
    }

    public async Task ClearAllAsync()
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        await _db.ExecuteAsync(
            "DELETE FROM ScheduledTransaction WHERE OwnerUserId = ?",
            ownerUserId);
    }

    public Task CloseAsync() => _db.CloseAsync();
}
