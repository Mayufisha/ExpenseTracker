using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public interface IAccountService
{
    AccountSession Session { get; }
    void SetServerUrl(string serverUrl);
    Task RegisterAsync(string email, string password);
    Task SignInAsync(string email, string password);
    void SignOut();
    Task PushToCloudAsync();
    Task<BackupImportResult> PullFromCloudAsync();
}
