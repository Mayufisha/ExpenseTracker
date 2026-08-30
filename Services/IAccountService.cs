using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public interface IAccountService
{
    AccountSession Session { get; }
    string BackendName { get; }
    Task InitializeAsync();
    Task RegisterAsync(string email, string password);
    Task SignInAsync(string email, string password);
    Task SignOutAsync();
    Task PushToCloudAsync();
    Task<BackupImportResult> PullFromCloudAsync();
}
