using System.Globalization;
using System.Security.Cryptography;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public class StatementImportService : IStatementImportService
{
    private readonly IFinancialAccountService _accountService;
    private readonly IExpenseService _expenseService;
    private readonly ICloudStatementSyncService _cloudSyncService;
    private readonly IUserDataContext _userContext;
    private readonly string _statementDirectory;
    private readonly SemaphoreSlim _reclassificationLock = new(1, 1);
    private string _reclassifiedUserId = string.Empty;

    public StatementImportService(
        IFinancialAccountService accountService,
        IExpenseService expenseService,
        ICloudStatementSyncService cloudSyncService,
        IUserDataContext userContext,
        string statementDirectory)
    {
        _accountService = accountService;
        _expenseService = expenseService;
        _cloudSyncService = cloudSyncService;
        _userContext = userContext;
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

        var ownerUserId = _userContext.RequireCurrentUserId();
        if (!account.OwnerUserId.Equals(ownerUserId, StringComparison.Ordinal))
            throw new InvalidOperationException("This financial account does not belong to the signed-in user.");

        var accountDirectory = Path.Combine(_statementDirectory, ownerUserId, account.Id.ToString());
        Directory.CreateDirectory(accountDirectory);
        var storedPath = Path.Combine(accountDirectory, $"{Guid.NewGuid():N}{extension}");

        try
        {
            await StatementFileValidator.CopyAndValidateAsync(sourceStream, storedPath, extension);
            var fileHash = await CalculateHashAsync(storedPath);
            if (await _accountService.HasStatementAsync(account.Id, fileHash))
                throw new InvalidOperationException("This statement is already attached to the account.");

            var importedCount = await ImportTransactionsAsync(
                account,
                storedPath,
                originalFileName,
                extension);

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

            var cloudMessage = string.Empty;
            try
            {
                var uploaded = await _cloudSyncService.SyncPendingAsync();
                cloudMessage = uploaded > 0 ? " Synced securely to Supabase." : string.Empty;
            }
            catch
            {
                cloudMessage = " Saved locally; cloud upload will retry on the next upload sync.";
            }

            return new StatementImportResult
            {
                ImportedTransactionCount = importedCount,
                TransactionsImported = importedCount > 0,
                Message = $"Attached {extension.TrimStart('.').ToUpperInvariant()} statement and imported {importedCount} transactions.{cloudMessage}"
            };
        }
        catch
        {
            if (File.Exists(storedPath)) File.Delete(storedPath);
            throw;
        }
    }

    public async Task<int> ReclassifyAttachedTransactionsAsync()
    {
        var ownerUserId = _userContext.RequireCurrentUserId();
        await _reclassificationLock.WaitAsync();
        try
        {
            if (_reclassifiedUserId.Equals(ownerUserId, StringComparison.Ordinal)) return 0;

            var correctedCount = await ReclassifyCurrentUserAsync();
            _reclassifiedUserId = ownerUserId;
            return correctedCount;
        }
        finally
        {
            _reclassificationLock.Release();
        }
    }

    private async Task<int> ReclassifyCurrentUserAsync()
    {
        var accounts = (await _accountService.GetAccountsAsync()).ToDictionary(account => account.Id);
        var statements = await _accountService.GetAllStatementsAsync();
        var transactions = await _expenseService.GetTransactionsAsync();
        var matchedTransactionIds = new HashSet<int>();
        var correctedCount = 0;

        foreach (var statement in statements.Where(item => item.ImportedTransactionCount > 0))
        {
            if (!accounts.TryGetValue(statement.FinancialAccountId, out var account)
                || !File.Exists(statement.StoredFilePath))
                continue;

            IReadOnlyList<ParsedStatementTransaction> parsedTransactions;
            try
            {
                var extension = Path.GetExtension(statement.StoredFilePath).ToLowerInvariant();
                parsedTransactions = extension == ".pdf"
                    ? await PdfStatementParser.ParseAsync(statement.StoredFilePath, account.ParsedAmountConvention)
                    : await CsvStatementParser.ParseAsync(statement.StoredFilePath, account.ParsedAmountConvention);
            }
            catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or InvalidDataException)
            {
                continue;
            }

            var existingByKey = transactions
                .Where(transaction => transaction.FinancialAccountId == account.Id
                    && transaction.StatementFileName.Equals(
                        statement.OriginalFileName,
                        StringComparison.OrdinalIgnoreCase)
                    && !matchedTransactionIds.Contains(transaction.Id))
                .GroupBy(transaction => CreateTransactionKey(
                    transaction.Date,
                    transaction.Amount,
                    transaction.Note))
                .ToDictionary(
                    group => group.Key,
                    group => new Queue<Transaction>(group.OrderBy(transaction => transaction.Id)));

            foreach (var parsed in parsedTransactions)
            {
                var key = CreateTransactionKey(parsed.Date, parsed.Amount, parsed.Description);
                if (!existingByKey.TryGetValue(key, out var matches) || matches.Count == 0)
                    continue;

                var transaction = matches.Dequeue();
                matchedTransactionIds.Add(transaction.Id);
                if (transaction.ParsedType == parsed.Type) continue;

                transaction.ParsedType = parsed.Type;
                await _expenseService.AddOrUpdateTransactionAsync(transaction);
                correctedCount++;
            }
        }

        return correctedCount;
    }

    private async Task<int> ImportTransactionsAsync(
        FinancialAccount account,
        string storedPath,
        string originalFileName,
        string extension)
    {
        var parsedTransactions = extension == ".pdf"
            ? await PdfStatementParser.ParseAsync(storedPath, account.ParsedAmountConvention)
            : await CsvStatementParser.ParseAsync(storedPath, account.ParsedAmountConvention);
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

    private static string CreateTransactionKey(DateTime date, decimal amount, string description)
    {
        var normalizedDescription = string.Join(
            ' ',
            description.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.Join(
            '|',
            date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            amount.ToString("0.############################", CultureInfo.InvariantCulture),
            normalizedDescription.ToUpperInvariant());
    }
}
