using SQLite;

namespace ExpenseTracker.Models;

public class Transaction
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public decimal Amount { get; set; }

    // Legacy compatibility for existing rows; new logic uses Type.
    public bool IsIncome { get; set; }

    public string Type { get; set; } = TransactionType.Expense.ToString();
    public int CategoryId { get; set; }
    public DateTime Date { get; set; }
    public string Note { get; set; } = string.Empty;

    [Ignore]
    public Category? Category { get; set; }

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
