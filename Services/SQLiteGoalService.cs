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

    async Task InitAsync()
    {
        if (_initialized) return;
        await _db.CreateTableAsync<Goal>();
        _initialized = true;
    }

    public async Task<IReadOnlyList<Goal>> GetGoalsAsync()
    {
        await InitAsync();
        return await _db.Table<Goal>().OrderBy(g => g.Deadline).ToListAsync();
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
}
