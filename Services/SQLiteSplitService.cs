using ExpenseTracker.Models;
using SQLite;

namespace ExpenseTracker.Services;

public sealed class SQLiteSplitService : ISplitService
{
    private readonly SQLiteAsyncConnection _db;
    private bool _initialized;

    public SQLiteSplitService(string databasePath)
    {
        _db = new SQLiteAsyncConnection(databasePath);
    }

    private async Task InitAsync()
    {
        if (_initialized) return;
        await _db.CreateTableAsync<ExpenseSplit>();
        await _db.CreateTableAsync<SplitParticipant>();
        _initialized = true;
    }

    public async Task<IReadOnlyList<ExpenseSplit>> GetSplitsAsync()
    {
        await InitAsync();
        var splits = await _db.Table<ExpenseSplit>()
            .OrderByDescending(split => split.UpdatedAt)
            .ToListAsync();
        var participants = await _db.Table<SplitParticipant>().ToListAsync();

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
            await _db.InsertAsync(participant);
        }

        split.Participants = participants.ToList();
        return split;
    }

    public async Task UpdateParticipantAsync(SplitParticipant participant)
    {
        await InitAsync();
        if (participant.Id == 0)
            await _db.InsertAsync(participant);
        else
            await _db.UpdateAsync(participant);

        await _db.ExecuteAsync(
            "UPDATE ExpenseSplit SET UpdatedAt = ? WHERE SyncId = ?",
            DateTime.UtcNow,
            participant.ExpenseSplitSyncId);
    }

    public async Task DeleteSplitAsync(string splitSyncId)
    {
        await InitAsync();
        await _db.ExecuteAsync(
            "DELETE FROM SplitParticipant WHERE ExpenseSplitSyncId = ?",
            splitSyncId);
        await _db.ExecuteAsync("DELETE FROM ExpenseSplit WHERE SyncId = ?", splitSyncId);
    }

    public async Task ClearAllAsync()
    {
        await InitAsync();
        await _db.DeleteAllAsync<SplitParticipant>();
        await _db.DeleteAllAsync<ExpenseSplit>();
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
