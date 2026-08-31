using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal sealed class SecurityEventRecorder
{
    private const long MaximumLogBytes = 5 * 1024 * 1024;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<SecurityEventRecorder> _logger;
    private readonly string _filePath;

    public SecurityEventRecorder(ILogger<SecurityEventRecorder> logger)
    {
        _logger = logger;
        var directory = Environment.GetEnvironmentVariable("MONEY_MANAGER_DATA_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MoneyManager",
                "LocalServer");
        }

        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "security-events.jsonl");
    }

    public async Task RecordAsync(HttpContext context, string eventType)
    {
        var fingerprintInput = string.Join(
            '|',
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            context.Request.Headers.UserAgent.ToString());
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput)))[..16];
        var entry = new SecurityAuditEvent(
            DateTimeOffset.UtcNow,
            eventType,
            context.Request.Method,
            fingerprint,
            context.TraceIdentifier);

        await _gate.WaitAsync();
        try
        {
            RotateIfNeeded();
            await File.AppendAllTextAsync(
                _filePath,
                JsonSerializer.Serialize(entry) + Environment.NewLine);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not write a local security audit event");
        }
        finally
        {
            _gate.Release();
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_filePath) || new FileInfo(_filePath).Length < MaximumLogBytes) return;
        File.Move(_filePath, $"{_filePath}.1", true);
    }

    private sealed record SecurityAuditEvent(
        DateTimeOffset TimestampUtc,
        string EventType,
        string Method,
        string ClientFingerprint,
        string TraceId);
}
