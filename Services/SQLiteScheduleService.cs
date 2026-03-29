using ExpenseTracker.Models;
using SQLite;

namespace ExpenseTracker.Services;

public class SQLiteScheduleService : IScheduleService
{
    private readonly SQLiteAsyncConnection _db;
    private bool _initialized;

    public SQLiteScheduleService(string databasePath)
    {
        _db = new SQLiteAsyncConnection(databasePath);
    }

    private async Task InitAsync()
    {
        if (_initialized) return;

        await _db.CreateTableAsync<ScheduledTransaction>();

        _initialized = true;
    }

    public async Task<IReadOnlyList<ScheduledTransaction>> GetScheduledAsync()
    {
        await InitAsync();
        return await _db.Table<ScheduledTransaction>()
                        .OrderBy(s => s.ScheduledDate)
                        .ToListAsync();
    }

    public async Task AddOrUpdateAsync(ScheduledTransaction scheduled)
    {
        await InitAsync();

        if (scheduled.Id == 0)
            await _db.InsertAsync(scheduled);
        else
            await _db.UpdateAsync(scheduled);
    }

    public async Task DeleteAsync(int id)
    {
        await InitAsync();
        await _db.DeleteAsync<ScheduledTransaction>(id);
    }

    public async Task ClearAllAsync()
    {
        await InitAsync();
        await _db.DeleteAllAsync<ScheduledTransaction>();
    }
}
