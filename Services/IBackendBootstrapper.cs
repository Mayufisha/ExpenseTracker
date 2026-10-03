namespace ExpenseTracker.Services;

public interface IBackendBootstrapper
{
    Task EnsureReadyAsync(CancellationToken cancellationToken = default);
    void Stop();
}
