namespace ExpenseTracker.Models;

public class ParsedStatementTransaction
{
    public DateTime Date { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public TransactionType Type { get; init; }
}
