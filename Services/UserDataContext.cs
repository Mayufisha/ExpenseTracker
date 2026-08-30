namespace ExpenseTracker.Services;

public sealed class UserDataContext : IUserDataContext
{
    public string CurrentUserId { get; private set; } = string.Empty;

    public void SetCurrentUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("A valid user ID is required for local data access.");

        CurrentUserId = userId.Trim();
    }

    public void Clear() => CurrentUserId = string.Empty;

    public string RequireCurrentUserId() => string.IsNullOrWhiteSpace(CurrentUserId)
        ? throw new InvalidOperationException("Sign in before accessing financial data.")
        : CurrentUserId;
}
