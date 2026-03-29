using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Tests.TestDoubles;

internal sealed class FakeScheduleService : IScheduleService
{
    private readonly List<ScheduledTransaction> _scheduled;

    public FakeScheduleService(IEnumerable<ScheduledTransaction> scheduled)
    {
        _scheduled = scheduled.ToList();
    }

    public Task<IReadOnlyList<ScheduledTransaction>> GetScheduledAsync()
    {
        return Task.FromResult<IReadOnlyList<ScheduledTransaction>>(_scheduled
            .OrderBy(s => s.ScheduledDate)
            .ToList());
    }

    public Task AddOrUpdateAsync(ScheduledTransaction scheduled)
    {
        if (scheduled.Id == 0)
        {
            scheduled.Id = _scheduled.Count == 0 ? 1 : _scheduled.Max(s => s.Id) + 1;
            _scheduled.Add(scheduled);
            return Task.CompletedTask;
        }

        var index = _scheduled.FindIndex(s => s.Id == scheduled.Id);
        if (index >= 0)
        {
            _scheduled[index] = scheduled;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id)
    {
        _scheduled.RemoveAll(s => s.Id == id);
        return Task.CompletedTask;
    }

    public Task ClearAllAsync()
    {
        _scheduled.Clear();
        return Task.CompletedTask;
    }
}
