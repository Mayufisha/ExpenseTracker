namespace ExpenseTracker.Services;

public sealed class LocalServerOptions
{
    public const string DefaultBaseUrl = "http://127.0.0.1:5088";

    public string BaseUrl { get; init; } = DefaultBaseUrl;
}
