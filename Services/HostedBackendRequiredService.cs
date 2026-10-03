using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public sealed class HostedBackendRequiredService : IAccountService, IBackendBootstrapper
{
    public const string ConfigurationMessage =
        "This mobile build is missing its hosted backend configuration. "
        + "Install an official test or release build configured for Supabase.";

    public AccountSession Session { get; } = new();
    public string BackendName => "Hosted backend required";

    public Task EnsureReadyAsync(CancellationToken cancellationToken = default) => Fail(cancellationToken);
    public Task InitializeAsync() => Fail();
    public Task RegisterAsync(string email, string password) => Fail();
    public Task SignInAsync(string email, string password) => Fail();
    public Task SignOutAsync() => Fail();
    public Task PushToCloudAsync() => Fail();
    public Task<BackupImportResult> PullFromCloudAsync() => Fail<BackupImportResult>();
    public void Stop()
    {
    }

    private static Task Fail(CancellationToken cancellationToken = default) =>
        cancellationToken.IsCancellationRequested
            ? Task.FromCanceled(cancellationToken)
            : Task.FromException(new BackendConfigurationException(ConfigurationMessage));

    private static Task<T> Fail<T>() =>
        Task.FromException<T>(new BackendConfigurationException(ConfigurationMessage));
}
