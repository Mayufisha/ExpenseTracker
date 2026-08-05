namespace ExpenseTracker.Models;

public class StatementImportResult
{
    public int ImportedTransactionCount { get; init; }
    public bool TransactionsImported { get; init; }
    public string Message { get; init; } = string.Empty;
}
