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

    async Task InitAsync()
    {
        if (_initialized) return;

        await _db.CreateTableAsync<ScheduledTransaction>();

        var count = await _db.Table<ScheduledTransaction>().CountAsync();
        if (count == 0)
        {
            var seed = new[]
            {
                new ScheduledTransaction
                {
                    Note = "Rent",
                    Amount = 1000,
                    IsIncome = false,
                    ScheduledDate = DateTime.Today.AddDays(7),
                    Frequency = "Monthly"
                },
                new ScheduledTransaction
                {
                    Note = "Gym membership",
                    Amount = 40,
                    IsIncome = false,
                    ScheduledDate = DateTime.Today.AddDays(3),
                    Frequency = "Monthly"
                }
            };
            await _db.InsertAllAsync(seed);
        }

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
}
