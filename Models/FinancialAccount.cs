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
    public decimal? CurrentBalance { get; set; }
    public DateTime? BalanceAsOf { get; set; }
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
    public bool IsLiabilityAccount =>
        AccountType.Contains("credit", StringComparison.OrdinalIgnoreCase)
        || AccountType.Contains("loan", StringComparison.OrdinalIgnoreCase)
        || AccountType.Contains("mortgage", StringComparison.OrdinalIgnoreCase)
        || AccountType.Contains("line of credit", StringComparison.OrdinalIgnoreCase);

    [Ignore]
    public decimal AssetValue => CurrentBalance switch
    {
        null => 0m,
        < 0m when IsLiabilityAccount => Math.Abs(CurrentBalance.Value),
        > 0m when !IsLiabilityAccount => CurrentBalance.Value,
        _ => 0m
    };

    [Ignore]
    public decimal LiabilityValue => CurrentBalance switch
    {
        null => 0m,
        > 0m when IsLiabilityAccount => CurrentBalance.Value,
        < 0m when !IsLiabilityAccount => Math.Abs(CurrentBalance.Value),
        _ => 0m
    };

    [Ignore]
    public string CurrentBalanceDisplay => CurrentBalance.HasValue
        ? IsLiabilityAccount
            ? $"${CurrentBalance.Value:F2} owed"
            : $"${CurrentBalance.Value:F2}"
        : "Balance not set";

    [Ignore]
    public string BalanceAsOfDisplay => BalanceAsOf.HasValue
        ? $"As of {BalanceAsOf.Value:MMM d, yyyy}"
        : "Set a current balance to calculate net worth";

    [Ignore]
    public string DisplayName => string.IsNullOrWhiteSpace(LastFour)
        ? $"{InstitutionName} - {AccountName}"
        : $"{InstitutionName} - {AccountName} (ending {LastFour})";
}
