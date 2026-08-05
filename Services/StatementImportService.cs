using System.Security.Cryptography;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public class StatementImportService : IStatementImportService
{
    private readonly IFinancialAccountService _accountService;
    private readonly IExpenseService _expenseService;
    private readonly string _statementDirectory;

    public StatementImportService(
        IFinancialAccountService accountService,
        IExpenseService expenseService,
        string statementDirectory)
    {
        _accountService = accountService;
        _expenseService = expenseService;
        _statementDirectory = statementDirectory;
    }

    public async Task<StatementImportResult> AttachAndImportAsync(
        FinancialAccount account,
        Stream sourceStream,
        string originalFileName)
    {
        if (account.Id == 0)
            throw new InvalidOperationException("Save the financial account before attaching a statement.");

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (extension is not ".csv" and not ".pdf")
            throw new InvalidDataException("Only CSV and PDF statements are supported.");

        var accountDirectory = Path.Combine(_statementDirectory, account.Id.ToString());
        Directory.CreateDirectory(accountDirectory);
        var storedPath = Path.Combine(accountDirectory, $"{Guid.NewGuid():N}{extension}");

        await using (var destination = File.Create(storedPath))
        {
            await sourceStream.CopyToAsync(destination);
        }

        try
        {
            var fileHash = await CalculateHashAsync(storedPath);
            if (await _accountService.HasStatementAsync(account.Id, fileHash))
                throw new InvalidOperationException("This statement is already attached to the account.");

            var importedCount = 0;
            if (extension == ".csv")
            {
                importedCount = await ImportCsvTransactionsAsync(account, storedPath, originalFileName);
            }

            await _accountService.AddStatementAsync(new StatementAttachment
            {
                FinancialAccountId = account.Id,
                OriginalFileName = originalFileName,
                StoredFilePath = storedPath,
                FileType = extension.TrimStart('.').ToUpperInvariant(),
                FileHash = fileHash,
                ImportedTransactionCount = importedCount,
                AttachedAt = DateTime.UtcNow
            });

            return new StatementImportResult
            {
                ImportedTransactionCount = importedCount,
                TransactionsImported = extension == ".csv",
                Message = extension == ".csv"
                    ? $"Attached statement and imported {importedCount} transactions."
                    : "Attached PDF statement. Use a CSV export to import transactions automatically."
            };
        }
        catch
        {
            if (File.Exists(storedPath)) File.Delete(storedPath);
            throw;
        }
    }

    private async Task<int> ImportCsvTransactionsAsync(
        FinancialAccount account,
        string storedPath,
        string originalFileName)
    {
        var parsedTransactions = await CsvStatementParser.ParseAsync(storedPath, account.AccountType);
        var categories = await _expenseService.GetCategoriesAsync();
        var fallbackCategory = categories.FirstOrDefault(c =>
            c.Name.Equals("Other", StringComparison.OrdinalIgnoreCase)) ?? categories.First();

        foreach (var item in parsedTransactions)
        {
            var transaction = new Transaction
            {
                Amount = item.Amount,
                Date = item.Date,
                Note = item.Description,
                CategoryId = fallbackCategory.Id,
                FinancialAccountId = account.Id,
                InstitutionName = account.InstitutionName,
                AccountName = account.AccountName,
                StatementFileName = originalFileName
            };
            transaction.ParsedType = item.Type;
            await _expenseService.AddOrUpdateTransactionAsync(transaction);
        }

        return parsedTransactions.Count;
    }

    private static async Task<string> CalculateHashAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash);
    }
}
