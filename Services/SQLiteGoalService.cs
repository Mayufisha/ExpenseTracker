using ExpenseTracker.Models;
using SQLite;

namespace ExpenseTracker.Services;

public class SQLiteGoalService : IGoalService
{
    private readonly SQLiteAsyncConnection _db;
    private readonly IUserDataContext _userContext;
    private bool _initialized;

    public SQLiteGoalService(string databasePath, IUserDataContext userContext)
    {
        _db = new SQLiteAsyncConnection(databasePath);
        _userContext = userContext;
    }

    private async Task InitAsync()
    {
        if (_initialized) return;

        await _db.CreateTableAsync<Goal>();
        await SQLiteSchema.EnsureOwnerColumnAsync(_db, nameof(Goal));

        _initialized = true;
    }

    public async Task<IReadOnlyList<Goal>> GetGoalsAsync()
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        var items = await _db.Table<Goal>()
            .Where(goal => goal.OwnerUserId == ownerUserId)
            .ToListAsync();
        return items
            .OrderBy(g => g.Deadline ?? DateTime.MaxValue)
            .ToList();
    }


    public async Task AddOrUpdateGoalAsync(Goal goal)
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        if (goal.Id != 0 && await _db.Table<Goal>()
                .Where(item => item.Id == goal.Id && item.OwnerUserId == ownerUserId)
                .CountAsync() == 0)
            throw new InvalidOperationException("This goal does not belong to the signed-in user.");
        goal.OwnerUserId = ownerUserId;

        if (goal.Id == 0)
            await _db.InsertAsync(goal);
        else
            await _db.UpdateAsync(goal);
    }

    public async Task DeleteGoalAsync(int id)
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        await _db.ExecuteAsync("DELETE FROM Goal WHERE Id = ? AND OwnerUserId = ?", id, ownerUserId);
    }

    public async Task ClearAllAsync()
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        await _db.ExecuteAsync("DELETE FROM Goal WHERE OwnerUserId = ?", ownerUserId);
    }

    public Task CloseAsync() => _db.CloseAsync();
}
