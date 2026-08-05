using System.Text.Json;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public class DataBackupService : IBackupService
{
    private readonly IExpenseService _expenseService;
    private readonly IGoalService _goalService;
    private readonly IScheduleService _scheduleService;
    private readonly IFinancialAccountService _financialAccountService;

    public DataBackupService(
        IExpenseService expenseService,
        IGoalService goalService,
        IScheduleService scheduleService,
        IFinancialAccountService financialAccountService)
    {
        _expenseService = expenseService;
        _goalService = goalService;
        _scheduleService = scheduleService;
        _financialAccountService = financialAccountService;
    }

    public async Task<string> ExportBackupAsync(string outputDirectory)
    {
        var backup = await CreateBackupAsync();

        Directory.CreateDirectory(outputDirectory);
        var fileName = $"expense-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
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

        return new DataBackup
        {
            Transactions = transactions
                .Select(t => new TransactionBackupItem
                {
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
            FinancialAccounts = financialAccounts.Select(a => new FinancialAccountBackupItem
            {
                InstitutionName = a.InstitutionName,
                AccountName = a.AccountName,
                AccountType = a.AccountType,
                LastFour = a.LastFour
            }).ToList(),
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

        var importedTransactions = 0;
        foreach (var item in backup.Transactions)
        {
            var category = categoryByName.TryGetValue(item.CategoryName.Trim(), out var found)
                ? found
                : fallbackCategory;

            var transaction = new Transaction
            {
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
            ImportedScheduledItems = importedScheduled
        };
    }

    public async Task ClearAllDataAsync()
    {
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
