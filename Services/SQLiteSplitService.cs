using ExpenseTracker.Models;
using SQLite;

namespace ExpenseTracker.Services;

public sealed class SQLiteSplitService : ISplitService
{
    private readonly SQLiteAsyncConnection _db;
    private readonly IUserDataContext _userContext;
    private bool _initialized;

    public SQLiteSplitService(string databasePath, IUserDataContext userContext)
    {
        _db = new SQLiteAsyncConnection(databasePath);
        _userContext = userContext;
    }

    private async Task InitAsync()
    {
        if (_initialized) return;
        await _db.CreateTableAsync<ExpenseSplit>();
        await _db.CreateTableAsync<SplitParticipant>();
        await SQLiteSchema.EnsureOwnerColumnAsync(_db, nameof(ExpenseSplit));
        await SQLiteSchema.EnsureOwnerColumnAsync(_db, nameof(SplitParticipant));
        _initialized = true;
    }

    public async Task<IReadOnlyList<ExpenseSplit>> GetSplitsAsync()
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        var splits = await _db.Table<ExpenseSplit>()
            .Where(split => split.OwnerUserId == ownerUserId)
            .OrderByDescending(split => split.UpdatedAt)
            .ToListAsync();
        var participants = await _db.Table<SplitParticipant>()
            .Where(participant => participant.OwnerUserId == ownerUserId)
            .ToListAsync();

        foreach (var split in splits)
        {
            split.Participants = participants
                .Where(participant => participant.ExpenseSplitSyncId == split.SyncId)
                .OrderBy(participant => participant.Name)
                .ToList();
        }

        return splits;
    }

    public async Task<ExpenseSplit> CreateSplitAsync(
        ExpenseSplit split,
        IReadOnlyList<SplitParticipant> participants)
    {
        await InitAsync();
        Validate(split, participants);
        var ownerUserId = _userContext.RequireCurrentUserId();
        split.OwnerUserId = ownerUserId;

        split.SyncId = string.IsNullOrWhiteSpace(split.SyncId)
            ? Guid.NewGuid().ToString("N")
            : split.SyncId;
        split.CreatedAt = split.CreatedAt == default ? DateTime.UtcNow : split.CreatedAt;
        split.UpdatedAt = DateTime.UtcNow;
        await _db.InsertAsync(split);

        foreach (var participant in participants)
        {
            participant.SyncId = string.IsNullOrWhiteSpace(participant.SyncId)
                ? Guid.NewGuid().ToString("N")
                : participant.SyncId;
            participant.ExpenseSplitSyncId = split.SyncId;
            participant.OwnerUserId = ownerUserId;
            await _db.InsertAsync(participant);
        }

        split.Participants = participants.ToList();
        return split;
    }

    public async Task UpdateParticipantAsync(SplitParticipant participant)
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        if (participant.Id != 0 && await _db.Table<SplitParticipant>()
                .Where(item => item.Id == participant.Id && item.OwnerUserId == ownerUserId)
                .CountAsync() == 0)
            throw new InvalidOperationException("This split participant does not belong to the signed-in user.");
        participant.OwnerUserId = ownerUserId;
        if (participant.Id == 0)
            await _db.InsertAsync(participant);
        else
            await _db.UpdateAsync(participant);

        await _db.ExecuteAsync(
            "UPDATE ExpenseSplit SET UpdatedAt = ? WHERE SyncId = ? AND OwnerUserId = ?",
            DateTime.UtcNow,
            participant.ExpenseSplitSyncId,
            ownerUserId);
    }

    public async Task DeleteSplitAsync(string splitSyncId)
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        await _db.ExecuteAsync(
            "DELETE FROM SplitParticipant WHERE ExpenseSplitSyncId = ? AND OwnerUserId = ?",
            splitSyncId,
            ownerUserId);
        await _db.ExecuteAsync(
            "DELETE FROM ExpenseSplit WHERE SyncId = ? AND OwnerUserId = ?",
            splitSyncId,
            ownerUserId);
    }

    public async Task ClearAllAsync()
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        await _db.ExecuteAsync("DELETE FROM SplitParticipant WHERE OwnerUserId = ?", ownerUserId);
        await _db.ExecuteAsync("DELETE FROM ExpenseSplit WHERE OwnerUserId = ?", ownerUserId);
    }

    public Task CloseAsync() => _db.CloseAsync();

    private static void Validate(ExpenseSplit split, IReadOnlyList<SplitParticipant> participants)
    {
        if (string.IsNullOrWhiteSpace(split.TransactionSyncId))
            throw new InvalidOperationException("Choose a transaction to split.");
        if (split.TotalAmount <= 0)
            throw new InvalidOperationException("The split total must be greater than zero.");
        if (participants.Count == 0)
            throw new InvalidOperationException("Add at least one person to this split.");
        if (participants.Any(participant =>
                string.IsNullOrWhiteSpace(participant.Name) || participant.AmountOwed <= 0))
            throw new InvalidOperationException("Every person needs a name and an amount greater than zero.");

        var sharedAmount = participants.Sum(participant => participant.AmountOwed);
        if (sharedAmount > split.TotalAmount)
            throw new InvalidOperationException("Participant shares cannot exceed the transaction total.");

        split.UserShare = split.TotalAmount - sharedAmount;
    }
}
