using System.Net.Mail;

namespace ExpenseTracker.Security;

public static class CredentialPolicy
{
    public const int MinimumPasswordLength = 12;
    public const int MaximumPasswordLength = 128;
    public const int MaximumEmailLength = 254;

    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > MaximumEmailLength)
            return false;

        return MailAddress.TryCreate(email.Trim(), out var parsed)
            && parsed.Address.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetPasswordError(string? password, bool enforceMinimumLength = true)
    {
        if (string.IsNullOrEmpty(password))
            return "Enter your password.";
        if (enforceMinimumLength && password.Length < MinimumPasswordLength)
            return $"Password must be at least {MinimumPasswordLength} characters.";
        if (password.Length > MaximumPasswordLength)
            return $"Password must not exceed {MaximumPasswordLength} characters.";
        return null;
    }
}
