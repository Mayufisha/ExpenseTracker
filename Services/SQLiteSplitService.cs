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
            .OrderByDescending(split => split.UpdatedAt).ToListAsync();
        var participants = await _db.Table<SplitParticipant>()
            .Where(participant => participant.OwnerUserId == ownerUserId).ToListAsync();
        foreach (var split in splits)
            split.Participants = participants.Where(participant => participant.ExpenseSplitSyncId == split.SyncId)
                .OrderBy(participant => participant.Name).ToList();
        return splits;
    }

    public async Task<ExpenseSplit> CreateSplitAsync(ExpenseSplit split, IReadOnlyList<SplitParticipant> participants)
    {
        await InitAsync();
        Validate(split, participants);
        var ownerUserId = _userContext.RequireCurrentUserId();
        split.OwnerUserId = ownerUserId;
        split.SyncId = string.IsNullOrWhiteSpace(split.SyncId) ? Guid.NewGuid().ToString("N") : split.SyncId;
        split.Title = split.Title.Trim();
        split.CreatedAt = split.CreatedAt == default ? DateTime.UtcNow : split.CreatedAt;
        split.UpdatedAt = DateTime.UtcNow;

        await _db.RunInTransactionAsync(database =>
        {
            if (database.Table<ExpenseSplit>().Count(item => item.OwnerUserId == ownerUserId
                    && (item.TransactionSyncId == split.TransactionSyncId || item.SyncId == split.SyncId)) > 0)
                throw new InvalidOperationException("This transaction already has a split.");
            database.Insert(split);
            foreach (var participant in participants)
            {
                participant.SyncId = string.IsNullOrWhiteSpace(participant.SyncId)
                    ? Guid.NewGuid().ToString("N") : participant.SyncId;
                if (database.Table<SplitParticipant>().Count(item => item.OwnerUserId == ownerUserId
                        && item.SyncId == participant.SyncId) > 0)
                    throw new InvalidOperationException("A participant identifier is already in use.");
                participant.Name = participant.Name.Trim();
                participant.Contact = participant.Contact?.Trim() ?? string.Empty;
                participant.ExpenseSplitSyncId = split.SyncId;
                participant.OwnerUserId = ownerUserId;
                database.Insert(participant);
            }
        });
        split.Participants = participants.ToList();
        return split;
    }

    public async Task UpdateParticipantAsync(SplitParticipant participant)
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        await _db.RunInTransactionAsync(database =>
        {
            var stored = database.Table<SplitParticipant>().FirstOrDefault(item =>
                item.Id == participant.Id && item.OwnerUserId == ownerUserId);
            if (stored == null || stored.SyncId != participant.SyncId
                || stored.ExpenseSplitSyncId != participant.ExpenseSplitSyncId)
                throw new InvalidOperationException("This split participant does not belong to the signed-in user or split.");
            var parent = database.Table<ExpenseSplit>().FirstOrDefault(item =>
                item.SyncId == stored.ExpenseSplitSyncId && item.OwnerUserId == ownerUserId);
            if (parent == null)
                throw new InvalidOperationException("The split no longer exists. Refresh SplitFare and try again.");
            if (participant.AmountOwed != stored.AmountOwed)
                throw new InvalidOperationException("Settlement updates cannot change a participant's share.");
            participant.OwnerUserId = ownerUserId;
            database.Update(participant);
            database.Execute("UPDATE ExpenseSplit SET UpdatedAt = ? WHERE SyncId = ? AND OwnerUserId = ?",
                DateTime.UtcNow, parent.SyncId, ownerUserId);
        });
    }

    public async Task DeleteSplitAsync(string splitSyncId)
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        await _db.RunInTransactionAsync(database =>
        {
            database.Execute("DELETE FROM SplitParticipant WHERE ExpenseSplitSyncId = ? AND OwnerUserId = ?", splitSyncId, ownerUserId);
            database.Execute("DELETE FROM ExpenseSplit WHERE SyncId = ? AND OwnerUserId = ?", splitSyncId, ownerUserId);
        });
    }

    public async Task ClearAllAsync()
    {
        await InitAsync();
        var ownerUserId = _userContext.RequireCurrentUserId();
        await _db.RunInTransactionAsync(database =>
        {
            database.Execute("DELETE FROM SplitParticipant WHERE OwnerUserId = ?", ownerUserId);
            database.Execute("DELETE FROM ExpenseSplit WHERE OwnerUserId = ?", ownerUserId);
        });
    }

    public Task CloseAsync() => _db.CloseAsync();

    private static void Validate(ExpenseSplit split, IReadOnlyList<SplitParticipant> participants)
    {
        if (string.IsNullOrWhiteSpace(split.TransactionSyncId))
            throw new InvalidOperationException("Choose a transaction to split.");
        if (split.TotalAmount <= 0 || decimal.Round(split.TotalAmount, 2) != split.TotalAmount)
            throw new InvalidOperationException("The split total must be greater than zero and use at most two decimal places.");
        if (string.IsNullOrWhiteSpace(split.Title) || split.Title.Length > 200)
            throw new InvalidOperationException("Enter a split name of at most 200 characters.");
        if (participants.Count is < 1 or > 100)
            throw new InvalidOperationException("Add between one and 100 people to this split.");
        if (participants.Any(person => string.IsNullOrWhiteSpace(person.Name) || person.Name.Length > 200
                || (person.Contact?.Length ?? 0) > 320 || person.AmountOwed <= 0
                || decimal.Round(person.AmountOwed, 2) != person.AmountOwed))
            throw new InvalidOperationException("Each person needs a name, a valid contact, and a positive share with at most two decimal places.");
        if (participants.Select(person => person.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != participants.Count)
            throw new InvalidOperationException("Each person must have a unique name.");
        var sharedAmount = participants.Sum(person => person.AmountOwed);
        if (sharedAmount > split.TotalAmount)
            throw new InvalidOperationException("Participant shares cannot exceed the transaction total.");
        split.UserShare = split.TotalAmount - sharedAmount;
    }
}
