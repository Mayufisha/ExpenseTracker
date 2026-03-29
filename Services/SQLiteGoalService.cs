using ExpenseTracker.Models;
using SQLite;

namespace ExpenseTracker.Services;

public class SQLiteGoalService : IGoalService
{
    private readonly SQLiteAsyncConnection _db;
    private bool _initialized;

    public SQLiteGoalService(string databasePath)
    {
        _db = new SQLiteAsyncConnection(databasePath);
    }

    private async Task InitAsync()
    {
        if (_initialized) return;

        await _db.CreateTableAsync<Goal>();

        _initialized = true;
    }

    public async Task<IReadOnlyList<Goal>> GetGoalsAsync()
    {
        await InitAsync();
        var items = await _db.Table<Goal>().ToListAsync();
        return items
            .OrderBy(g => g.Deadline ?? DateTime.MaxValue)
            .ToList();
    }


    public async Task AddOrUpdateGoalAsync(Goal goal)
    {
        await InitAsync();

        if (goal.Id == 0)
            await _db.InsertAsync(goal);
        else
            await _db.UpdateAsync(goal);
    }

    public async Task DeleteGoalAsync(int id)
    {
        await InitAsync();
        await _db.DeleteAsync<Goal>(id);
    }

    public async Task ClearAllAsync()
    {
        await InitAsync();
        await _db.DeleteAllAsync<Goal>();
    }
}
