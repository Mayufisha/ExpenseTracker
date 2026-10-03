namespace ExpenseTracker.Services;

public sealed class NoOpBackendBootstrapper : IBackendBootstrapper
{
    public Task EnsureReadyAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Stop()
    {
    }
}
