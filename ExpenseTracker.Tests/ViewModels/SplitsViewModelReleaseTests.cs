using ExpenseTracker.Models;
using ExpenseTracker.Services;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Tests.ViewModels;

public class SplitsViewModelReleaseTests
{
    [Fact]
    public async Task FailedRefresh_PreservesLoadedSplitsAndTotals_AndCanRetry()
    {
        var service = new FailingSplitService();
        var model = new SplitsViewModel(service);
        await model.LoadAsync();
        Assert.Single(model.Splits);
        Assert.Equal(12m, model.TotalOutstanding);
        service.Fail = true;
        await model.LoadAsync();
        Assert.Single(model.Splits);
        Assert.Equal(12m, model.TotalOutstanding);
        Assert.True(model.HasError);
        Assert.False(model.IsBusy);
        service.Fail = false;
        await model.LoadAsync();
        Assert.False(model.HasError);
        Assert.Single(model.Splits);
    }

    [Fact]
    public async Task EmptyAccount_ShowsZeroBalances()
    {
        var service = new FailingSplitService { Items = [] };
        var model = new SplitsViewModel(service);
        await model.LoadAsync();
        Assert.Empty(model.Splits);
        Assert.Equal(0m, model.TotalOutstanding);
        Assert.Equal(0m, model.TotalCollected);
        Assert.False(model.HasError);
    }

    private sealed class FailingSplitService : ISplitService
    {
        public bool Fail { get; set; }
        public IReadOnlyList<ExpenseSplit> Items { get; set; } =
            [new ExpenseSplit { Title = "Fare", TotalAmount = 24m, Participants = [new SplitParticipant { Name = "Alex", AmountOwed = 12m }] }];
        public Task<IReadOnlyList<ExpenseSplit>> GetSplitsAsync() => Fail
            ? Task.FromException<IReadOnlyList<ExpenseSplit>>(new IOException("Local test failure"))
            : Task.FromResult(Items);
        public Task<ExpenseSplit> CreateSplitAsync(ExpenseSplit split, IReadOnlyList<SplitParticipant> participants) => throw new NotSupportedException();
        public Task UpdateParticipantAsync(SplitParticipant participant) => throw new NotSupportedException();
        public Task DeleteSplitAsync(string splitSyncId) => throw new NotSupportedException();
        public Task ClearAllAsync() { Items = []; return Task.CompletedTask; }
    }
}
