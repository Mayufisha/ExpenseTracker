using ExpenseTracker.Models;
using ExpenseTracker.Services;
using SQLite;

namespace ExpenseTracker.Tests.Services;

public sealed class SplitFareReleaseTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"splitfare-release-{Guid.NewGuid():N}.db3");
    private readonly UserDataContext _user = new();
    private SQLiteSplitService _service = null!;
    public Task InitializeAsync()
    {
        _user.SetCurrentUser("owner-a");
        _service = new SQLiteSplitService(_path, _user);
        return Task.CompletedTask;
    }
    public async Task DisposeAsync()
    {
        await _service.CloseAsync();
        File.Delete(_path);
    }
    private Task<ExpenseSplit> Create(string transaction = "purchase", string name = "Alex", decimal share = 10m) =>
        _service.CreateSplitAsync(new ExpenseSplit { TransactionSyncId = transaction, Title = "Trip fare", TotalAmount = 30m },
            [new SplitParticipant { Name = name, AmountOwed = share }]);

    [Fact]
    public async Task DuplicateTransaction_IsRejectedWithoutCreatingExtraRows()
    {
        await Create();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create());
        Assert.Single(await _service.GetSplitsAsync());
    }
    [Fact]
    public async Task FailedParticipantInsert_RollsBackWholeSplit()
    {
        await _service.GetSplitsAsync();
        var database = new SQLiteAsyncConnection(_path);
        await database.ExecuteAsync("CREATE TRIGGER RejectTestParticipant BEFORE INSERT ON SplitParticipant WHEN NEW.Name = 'Reject' BEGIN SELECT RAISE(ABORT, 'test insertion failure'); END");
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => _service.CreateSplitAsync(
                new ExpenseSplit { TransactionSyncId = "rollback", Title = "Dinner", TotalAmount = 30m },
                [new SplitParticipant { Name = "Alex", AmountOwed = 10m }, new SplitParticipant { Name = "Reject", AmountOwed = 10m }]));
            Assert.Empty(await _service.GetSplitsAsync());
            Assert.Equal(0, await database.Table<SplitParticipant>().CountAsync());
        }
        finally { await database.CloseAsync(); }
    }
    [Fact]
    public async Task AccountSwitch_CannotSettleAnotherOwnersParticipant()
    {
        var split = await Create();
        var participant = Assert.Single(split.Participants);
        _user.SetCurrentUser("owner-b");
        participant.IsPaid = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateParticipantAsync(participant));
        _user.SetCurrentUser("owner-a");
        Assert.False(Assert.Single(Assert.Single(await _service.GetSplitsAsync()).Participants).IsPaid);
    }
    [Fact]
    public async Task Participant_CannotBeMovedBetweenSplits()
    {
        var first = await Create("first");
        var second = await Create("second");
        var participant = Assert.Single(first.Participants);
        participant.ExpenseSplitSyncId = second.SyncId;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateParticipantAsync(participant));
        Assert.All(await _service.GetSplitsAsync(), item => Assert.Single(item.Participants));
    }
    [Fact]
    public async Task DeletedSplit_CannotBeResurrectedByStaleSettlement()
    {
        var split = await Create();
        await _service.DeleteSplitAsync(split.SyncId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateParticipantAsync(Assert.Single(split.Participants)));
        Assert.Empty(await _service.GetSplitsAsync());
    }
    [Theory]
    [InlineData("10.001")]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task InvalidShares_AreRejected(string value)
    {
        var share = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(share: share));
        Assert.Empty(await _service.GetSplitsAsync());
    }
    [Fact]
    public async Task DuplicateNames_AreRejectedAfterTrimmingAndIgnoringCase()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateSplitAsync(
            new ExpenseSplit { TransactionSyncId = "names", Title = "Dinner", TotalAmount = 30m },
            [new SplitParticipant { Name = "Alex", AmountOwed = 10m }, new SplitParticipant { Name = " alex ", AmountOwed = 10m }]));
    }
    [Fact]
    public async Task Settlement_CannotChangeTheAllocatedAmount()
    {
        var participant = Assert.Single((await Create()).Participants);
        participant.AmountOwed = 100m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateParticipantAsync(participant));
        Assert.Equal(10m, Assert.Single(Assert.Single(await _service.GetSplitsAsync()).Participants).AmountOwed);
    }
    [Fact]
    public async Task SameTransactionIdentifier_IsIndependentBetweenAccounts()
    {
        await Create();
        _user.SetCurrentUser("owner-b");
        await Create();
        Assert.Single(await _service.GetSplitsAsync());
        await _service.ClearAllAsync();
        _user.SetCurrentUser("owner-a");
        Assert.Single(await _service.GetSplitsAsync());
    }
}
