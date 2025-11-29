using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public interface IScheduleService
{
    Task<IReadOnlyList<ScheduledTransaction>> GetScheduledAsync();
    Task AddOrUpdateAsync(ScheduledTransaction scheduled);
    Task DeleteAsync(int id);
}
