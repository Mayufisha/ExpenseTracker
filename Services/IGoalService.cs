using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public interface IGoalService
{
    Task<IReadOnlyList<Goal>> GetGoalsAsync();
    Task AddOrUpdateGoalAsync(Goal goal);
    Task DeleteGoalAsync(int id);
}