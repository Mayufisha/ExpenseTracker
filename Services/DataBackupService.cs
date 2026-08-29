using System.Text.Json;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public class DataBackupService : IBackupService
{
    private readonly IExpenseService _expenseService;
    private readonly IGoalService _goalService;
    private readonly IScheduleService _scheduleService;
    private readonly IFinancialAccountService _financialAccountService;
    private readonly ISplitService _splitService;

    public DataBackupService(
        IExpenseService expenseService,
        IGoalService goalService,
        IScheduleService scheduleService,
        IFinancialAccountService financialAccountService,
        ISplitService splitService)
    {
        _expenseService = expenseService;
        _goalService = goalService;
        _scheduleService = scheduleService;
        _financialAccountService = financialAccountService;
        _splitService = splitService;
    }

    public async Task<string> ExportBackupAsync(string outputDirectory)
    {
        var backup = await CreateBackupAsync();

        Directory.CreateDirectory(outputDirectory);
        var fileName = $"money-manager-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        var fullPath = Path.Combine(outputDirectory, fileName);
        var json = JsonSerializer.Serialize(backup, SerializerOptions);
        await File.WriteAllTextAsync(fullPath, json);

        return fullPath;
    }

    public async Task<BackupImportResult> ImportBackupAsync(string backupFilePath, bool clearExistingData = true)
    {
        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException("Backup file was not found.", backupFilePath);
        }

        var json = await File.ReadAllTextAsync(backupFilePath);
        var backup = JsonSerializer.Deserialize<DataBackup>(json, SerializerOptions)
            ?? throw new InvalidDataException("Backup file format is invalid.");

        return await ImportBackupAsync(backup, clearExistingData);
    }

    public async Task<DataBackup> CreateBackupAsync()
    {
        var transactions = await _expenseService.GetTransactionsAsync();
        var goals = await _goalService.GetGoalsAsync();
        var scheduledItems = await _scheduleService.GetScheduledAsync();
        var financialAccounts = await _financialAccountService.GetAccountsAsync();
        var statements = await _financialAccountService.GetAllStatementsAsync();
        var splits = await _splitService.GetSplitsAsync();
        var accountsById = financialAccounts.ToDictionary(account => account.Id);

        return new DataBackup
        {
            Transactions = transactions
                .Select(t => new TransactionBackupItem
                {
                    SyncId = t.SyncId,
                    Amount = t.Amount,
                    Type = t.ParsedType.ToString(),
                    Date = t.Date,
                    Note = t.Note,
                    CategoryName = t.Category?.Name ?? "Other",
                    InstitutionName = t.InstitutionName,
                    AccountName = t.AccountName,
                    StatementFileName = t.StatementFileName
                })
                .ToList(),
            ExpenseSplits = splits.Select(split => new ExpenseSplitBackupItem
            {
                SyncId = split.SyncId,
                TransactionSyncId = split.TransactionSyncId,
                Title = split.Title,
                TotalAmount = split.TotalAmount,
                UserShare = split.UserShare,
                Currency = split.Currency,
                SplitMethod = split.SplitMethod,
                CreatedAt = split.CreatedAt,
                UpdatedAt = split.UpdatedAt,
                Participants = split.Participants.Select(participant => new SplitParticipantBackupItem
                {
                    SyncId = participant.SyncId,
                    Name = participant.Name,
                    Contact = participant.Contact,
                    AmountOwed = participant.AmountOwed,
                    IsPaid = participant.IsPaid,
                    PaidAt = participant.PaidAt,
                    LastPaymentRequestAt = participant.LastPaymentRequestAt,
                    PaymentProvider = participant.PaymentProvider,
                    ExternalPaymentId = participant.ExternalPaymentId
                }).ToList()
            }).ToList(),
            FinancialAccounts = financialAccounts.Select(a => new FinancialAccountBackupItem
            {
                InstitutionName = a.InstitutionName,
                AccountName = a.AccountName,
                AccountType = a.AccountType,
                LastFour = a.LastFour
            }).ToList(),
            Statements = statements
                .Where(statement => accountsById.ContainsKey(statement.FinancialAccountId))
                .Select(statement =>
                {
                    var account = accountsById[statement.FinancialAccountId];
                    return new StatementBackupItem
                    {
                        InstitutionName = account.InstitutionName,
                        AccountName = account.AccountName,
                        OriginalFileName = statement.OriginalFileName,
                        CloudStoragePath = statement.CloudStoragePath,
                        FileType = statement.FileType,
                        FileHash = statement.FileHash,
                        AttachedAt = statement.AttachedAt,
                        ImportedTransactionCount = statement.ImportedTransactionCount
                    };
                })
                .ToList(),
            Goals = goals.Select(CloneGoal).ToList(),
            ScheduledItems = scheduledItems.Select(CloneScheduledItem).ToList()
        };
    }

    public async Task<BackupImportResult> ImportBackupAsync(DataBackup backup, bool clearExistingData = true)
    {
        if (clearExistingData)
        {
            await ClearAllDataAsync();
        }

        var categories = await _expenseService.GetCategoriesAsync();
        var categoryByName = categories
            .GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var fallbackCategory = categories.FirstOrDefault(c => c.Name.Equals("Other", StringComparison.OrdinalIgnoreCase))
            ?? categories.First();

        foreach (var account in backup.FinancialAccounts)
        {
            await _financialAccountService.AddOrUpdateAccountAsync(new FinancialAccount
            {
                InstitutionName = account.InstitutionName,
                AccountName = account.AccountName,
                AccountType = account.AccountType,
                LastFour = account.LastFour
            });
        }

        var importedAccounts = await _financialAccountService.GetAccountsAsync();

        var importedStatements = 0;
        foreach (var statement in backup.Statements)
        {
            var account = importedAccounts.FirstOrDefault(a =>
                a.InstitutionName.Equals(statement.InstitutionName, StringComparison.OrdinalIgnoreCase)
                && a.AccountName.Equals(statement.AccountName, StringComparison.OrdinalIgnoreCase));
            if (account == null || await _financialAccountService.HasStatementAsync(account.Id, statement.FileHash))
            {
                continue;
            }

            await _financialAccountService.AddStatementAsync(new StatementAttachment
            {
                FinancialAccountId = account.Id,
                OriginalFileName = statement.OriginalFileName,
                StoredFilePath = string.Empty,
                CloudStoragePath = statement.CloudStoragePath,
                FileType = statement.FileType,
                FileHash = statement.FileHash,
                AttachedAt = statement.AttachedAt,
                ImportedTransactionCount = statement.ImportedTransactionCount
            });
            importedStatements++;
        }

        var importedTransactions = 0;
        foreach (var item in backup.Transactions)
        {
            var category = categoryByName.TryGetValue(item.CategoryName.Trim(), out var found)
                ? found
                : fallbackCategory;

            var transaction = new Transaction
            {
                SyncId = string.IsNullOrWhiteSpace(item.SyncId)
                    ? Guid.NewGuid().ToString("N")
                    : item.SyncId,
                Amount = item.Amount,
                Date = item.Date,
                Note = item.Note ?? string.Empty,
                CategoryId = category.Id,
                Type = item.Type,
                InstitutionName = item.InstitutionName,
                AccountName = item.AccountName,
                StatementFileName = item.StatementFileName,
                FinancialAccountId = importedAccounts.FirstOrDefault(a =>
                    a.InstitutionName.Equals(item.InstitutionName, StringComparison.OrdinalIgnoreCase)
                    && a.AccountName.Equals(item.AccountName, StringComparison.OrdinalIgnoreCase))?.Id ?? 0
            };

            if (!Enum.TryParse<TransactionType>(transaction.Type, true, out var parsed))
            {
                parsed = TransactionType.Expense;
            }

            transaction.ParsedType = parsed;
            await _expenseService.AddOrUpdateTransactionAsync(transaction);
            importedTransactions++;
        }

        var importedSplits = 0;
        var transactionSyncIds = (await _expenseService.GetTransactionsAsync())
            .Select(transaction => transaction.SyncId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in backup.ExpenseSplits ?? new List<ExpenseSplitBackupItem>())
        {
            if (!transactionSyncIds.Contains(item.TransactionSyncId)) continue;

            var split = new ExpenseSplit
            {
                SyncId = item.SyncId,
                TransactionSyncId = item.TransactionSyncId,
                Title = item.Title,
                TotalAmount = item.TotalAmount,
                UserShare = item.UserShare,
                Currency = item.Currency,
                SplitMethod = item.SplitMethod,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            };
            var participants = (item.Participants ?? new List<SplitParticipantBackupItem>()).Select(participant => new SplitParticipant
            {
                SyncId = participant.SyncId,
                Name = participant.Name,
                Contact = participant.Contact,
                AmountOwed = participant.AmountOwed,
                IsPaid = participant.IsPaid,
                PaidAt = participant.PaidAt,
                LastPaymentRequestAt = participant.LastPaymentRequestAt,
                PaymentProvider = participant.PaymentProvider,
                ExternalPaymentId = participant.ExternalPaymentId
            }).ToList();
            await _splitService.CreateSplitAsync(split, participants);
            importedSplits++;
        }

        var importedGoals = 0;
        foreach (var goal in backup.Goals)
        {
            var insert = CloneGoal(goal);
            insert.Id = 0;
            await _goalService.AddOrUpdateGoalAsync(insert);
            importedGoals++;
        }

        var importedScheduled = 0;
        foreach (var scheduled in backup.ScheduledItems)
        {
            var insert = CloneScheduledItem(scheduled);
            insert.Id = 0;
            await _scheduleService.AddOrUpdateAsync(insert);
            importedScheduled++;
        }

        return new BackupImportResult
        {
            ImportedTransactions = importedTransactions,
            ImportedGoals = importedGoals,
            ImportedScheduledItems = importedScheduled,
            ImportedStatements = importedStatements,
            ImportedSplits = importedSplits
        };
    }

    public async Task ClearAllDataAsync()
    {
        await _splitService.ClearAllAsync();
        await _expenseService.ClearAllTransactionsAsync();
        await _goalService.ClearAllAsync();
        await _scheduleService.ClearAllAsync();
        await _financialAccountService.ClearAllAsync();
    }

    private static Goal CloneGoal(Goal goal)
    {
        return new Goal
        {
            Id = goal.Id,
            Name = goal.Name,
            TargetAmount = goal.TargetAmount,
            CurrentAmount = goal.CurrentAmount,
            Deadline = goal.Deadline,
            IsCompleted = goal.IsCompleted
        };
    }

    private static ScheduledTransaction CloneScheduledItem(ScheduledTransaction item)
    {
        return new ScheduledTransaction
        {
            Id = item.Id,
            Amount = item.Amount,
            IsIncome = item.IsIncome,
            CategoryId = item.CategoryId,
            ScheduledDate = item.ScheduledDate,
            Note = item.Note,
            Frequency = item.Frequency
        };
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
