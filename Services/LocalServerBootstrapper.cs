using System.Diagnostics;

namespace ExpenseTracker.Services;

public sealed class LocalServerBootstrapper : IBackendBootstrapper, IDisposable
{
    private const string ExecutableName = "ExpenseTracker.LocalServer.exe";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HttpClient _httpClient;
    private readonly string _healthUrl;
    private Process? _ownedProcess;

    public LocalServerBootstrapper(LocalServerOptions options, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _healthUrl = $"{options.BaseUrl.TrimEnd('/')}/health";
    }

    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        if (await IsHealthyAsync(cancellationToken)) return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (await IsHealthyAsync(cancellationToken)) return;

            var executablePath = ResolveExecutablePath();
            _ownedProcess = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }) ?? throw new InvalidOperationException("The bundled local server could not be started.");

            var deadline = DateTimeOffset.UtcNow.AddSeconds(12);
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_ownedProcess.HasExited)
                    throw new InvalidOperationException("The bundled local server stopped during startup.");
                if (await IsHealthyAsync(cancellationToken)) return;
                await Task.Delay(250, cancellationToken);
            }

            Stop();
            throw new InvalidOperationException("The bundled local server did not become ready in time.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Stop()
    {
        var process = _ownedProcess;
        _ownedProcess = null;
        if (process == null) return;

        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            process.Dispose();
        }
    }

    public void Dispose()
    {
        Stop();
        _gate.Dispose();
    }

    private async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(1));
        try
        {
            using var response = await _httpClient.GetAsync(_healthUrl, timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException
                                          or TaskCanceledException
                                          or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            return false;
        }
    }

    private static string ResolveExecutablePath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("MONEY_MANAGER_LOCAL_SERVER_EXE");
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "LocalServer", ExecutableName)
            : configuredPath;
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                "The local server is unavailable. Use a packaged local-test build or set "
                + "MONEY_MANAGER_LOCAL_SERVER_EXE to the trusted server executable.");
        }

        return fullPath;
    }
}
