namespace ExpenseTracker.Services;

public interface IUserDataContext
{
    string CurrentUserId { get; }
    void SetCurrentUser(string userId);
    void Clear();
    string RequireCurrentUserId();
}
