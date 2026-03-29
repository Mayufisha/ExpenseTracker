namespace ExpenseTracker.Models;

public class AccountSession
{
    public string ServerUrl { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;

    public bool IsSignedIn => !string.IsNullOrWhiteSpace(AccessToken);
}
