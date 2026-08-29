using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public interface ISplitService
{
    Task<IReadOnlyList<ExpenseSplit>> GetSplitsAsync();
    Task<ExpenseSplit> CreateSplitAsync(ExpenseSplit split, IReadOnlyList<SplitParticipant> participants);
    Task UpdateParticipantAsync(SplitParticipant participant);
    Task DeleteSplitAsync(string splitSyncId);
    Task ClearAllAsync();
}
