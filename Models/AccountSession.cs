namespace ExpenseTracker.Models;

public class AccountSession
{
    public string ProjectUrl { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }

    public bool IsConfigured =>
        Uri.TryCreate(ProjectUrl, UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(PublishableKey);

    public bool IsSignedIn =>
        !string.IsNullOrWhiteSpace(UserId)
        && !string.IsNullOrWhiteSpace(AccessToken);
}
