using SQLite;

namespace ExpenseTracker.Models;

public class FinancialAccount
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string InstitutionName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = "Bank";
    public string LastFour { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public int StatementCount { get; set; }

    [Ignore]
    public string DisplayName => string.IsNullOrWhiteSpace(LastFour)
        ? $"{InstitutionName} - {AccountName}"
        : $"{InstitutionName} - {AccountName} (•••• {LastFour})";
}
