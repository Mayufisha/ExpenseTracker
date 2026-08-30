using SQLite;

namespace ExpenseTracker.Models;

public class FinancialAccount
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    public string InstitutionName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = "Bank";
    public string AmountConvention { get; set; } = string.Empty;
    public string LastFour { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public int StatementCount { get; set; }

    [Ignore]
    public StatementAmountConvention ParsedAmountConvention
    {
        get
        {
            if (Enum.TryParse(AmountConvention, true, out StatementAmountConvention parsed))
                return parsed;

            return AccountType.Contains("credit", StringComparison.OrdinalIgnoreCase)
                ? StatementAmountConvention.PositiveAmountsAreExpenses
                : StatementAmountConvention.NegativeAmountsAreExpenses;
        }
        set => AmountConvention = value.ToString();
    }

    [Ignore]
    public string AmountConventionDisplay => ParsedAmountConvention == StatementAmountConvention.PositiveAmountsAreExpenses
        ? "Positive amounts = expenses"
        : "Negative amounts = expenses";

    [Ignore]
    public string DisplayName => string.IsNullOrWhiteSpace(LastFour)
        ? $"{InstitutionName} - {AccountName}"
        : $"{InstitutionName} - {AccountName} (ending {LastFour})";
}
