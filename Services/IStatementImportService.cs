using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public interface IStatementImportService
{
    Task<StatementImportResult> AttachAndImportAsync(
        FinancialAccount account,
        Stream sourceStream,
        string originalFileName);
}
