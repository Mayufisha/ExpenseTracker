using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Tests.Services;

public class SQLiteSplitServiceTests
{
    static SQLiteSplitServiceTests()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    [Fact]
    public async Task CreateSplitAsync_PersistsParticipantsAndCalculatesUserShare()
    {
        var (service, path) = CreateService();

        try
        {
            var split = await service.CreateSplitAsync(
                new ExpenseSplit
                {
                    TransactionSyncId = "transaction-1",
                    Title = "Dinner",
                    TotalAmount = 90m
                },
                new[]
                {
                    new SplitParticipant { Name = "Alex", AmountOwed = 30m },
                    new SplitParticipant { Name = "Morgan", AmountOwed = 30m }
                });

            var stored = Assert.Single(await service.GetSplitsAsync());
            Assert.Equal(split.SyncId, stored.SyncId);
            Assert.Equal(30m, stored.UserShare);
            Assert.Equal(2, stored.Participants.Count);
            Assert.Equal(60m, stored.AmountOutstanding);
        }
        finally
        {
            await service.CloseAsync();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task UpdateParticipantAsync_PersistsSettlementState()
    {
        var (service, path) = CreateService();

        try
        {
            var created = await service.CreateSplitAsync(
                new ExpenseSplit
                {
                    TransactionSyncId = "transaction-2",
                    Title = "Taxi",
                    TotalAmount = 40m
                },
                new[] { new SplitParticipant { Name = "Taylor", AmountOwed = 20m } });
            var participant = Assert.Single(created.Participants);
            participant.IsPaid = true;
            participant.PaidAt = DateTime.UtcNow;

            await service.UpdateParticipantAsync(participant);

            var stored = Assert.Single(await service.GetSplitsAsync());
            Assert.True(stored.IsSettled);
            Assert.Equal(20m, stored.AmountCollected);
        }
        finally
        {
            await service.CloseAsync();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CreateSplitAsync_RejectsSharesAboveTransactionTotal()
    {
        var (service, path) = CreateService();

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateSplitAsync(
                new ExpenseSplit
                {
                    TransactionSyncId = "transaction-3",
                    Title = "Tickets",
                    TotalAmount = 50m
                },
                new[] { new SplitParticipant { Name = "Jordan", AmountOwed = 55m } }));
        }
        finally
        {
            await service.CloseAsync();
            File.Delete(path);
        }
    }

    private static (SQLiteSplitService Service, string Path) CreateService()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"money-manager-splits-{Guid.NewGuid():N}.db3");
        return (new SQLiteSplitService(path), path);
    }
}
