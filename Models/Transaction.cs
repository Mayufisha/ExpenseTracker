using SQLite;

namespace ExpenseTracker.Models;

public class Transaction
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string SyncId { get; set; } = Guid.NewGuid().ToString("N");

    public decimal Amount { get; set; }

    // Legacy compatibility for existing rows; new logic uses Type.
    public bool IsIncome { get; set; }

    public string Type { get; set; } = TransactionType.Expense.ToString();
    public int CategoryId { get; set; }
    public DateTime Date { get; set; }
    public string Note { get; set; } = string.Empty;
    public int FinancialAccountId { get; set; }
    public string InstitutionName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string StatementFileName { get; set; } = string.Empty;

    [Ignore]
    public Category? Category { get; set; }

    [Ignore]
    public string SourceDisplay => string.IsNullOrWhiteSpace(InstitutionName)
        ? "Manual entry"
        : $"{InstitutionName} - {AccountName}";

    [Ignore]
    public TransactionType ParsedType
    {
        get
        {
            if (Enum.TryParse(Type, true, out TransactionType parsed))
            {
                return parsed;
            }

            return IsIncome ? TransactionType.Income : TransactionType.Expense;
        }
        set
        {
            Type = value.ToString();
            IsIncome = value == TransactionType.Income;
        }
    }
}
