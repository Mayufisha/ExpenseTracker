using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public static class DataBackupValidator
{
    public const long MaximumBackupFileSize = 5 * 1024 * 1024;
    private const int MaximumTransactions = 50_000;
    private const int MaximumAccounts = 1_000;
    private const int MaximumStatements = 5_000;
    private const int MaximumGoals = 5_000;
    private const int MaximumScheduledItems = 10_000;
    private const int MaximumSplits = 20_000;
    private const int MaximumParticipantsPerSplit = 100;

    public static void Validate(DataBackup backup)
    {
        if (backup.Version is < 1 or > 4)
            throw new InvalidDataException("This backup version is not supported.");

        EnsureCollection(backup.Transactions, MaximumTransactions, "transactions");
        EnsureCollection(backup.FinancialAccounts, MaximumAccounts, "financial accounts");
        EnsureCollection(backup.Statements, MaximumStatements, "statements");
        EnsureCollection(backup.Goals, MaximumGoals, "goals");
        EnsureCollection(backup.ScheduledItems, MaximumScheduledItems, "scheduled items");
        EnsureCollection(backup.ExpenseSplits, MaximumSplits, "splits");

        foreach (var item in backup.Transactions)
        {
            EnsureIdentifier(item.SyncId, "transaction identifier");
            EnsureAmount(item.Amount, "transaction amount");
            EnsureLength(item.Note, 2_000, "transaction note");
            EnsureLength(item.CategoryName, 100, "category name");
            EnsureLength(item.InstitutionName, 200, "institution name");
            EnsureLength(item.AccountName, 200, "account name");
            EnsureFileName(item.StatementFileName, "statement filename");
            if (!Enum.TryParse<TransactionType>(item.Type, true, out _))
                throw new InvalidDataException("A transaction contains an invalid type.");
        }

        foreach (var account in backup.FinancialAccounts)
        {
            EnsureRequiredLength(account.InstitutionName, 200, "institution name");
            EnsureRequiredLength(account.AccountName, 200, "account name");
            EnsureLength(account.AccountType, 50, "account type");
            EnsureLength(account.AmountConvention, 50, "amount convention");
            if (account.LastFour.Length > 0
                && (account.LastFour.Length != 4 || account.LastFour.Any(character => !char.IsDigit(character))))
                throw new InvalidDataException("An account contains an invalid last-four value.");
        }

        foreach (var statement in backup.Statements)
        {
            EnsureRequiredLength(statement.InstitutionName, 200, "statement institution");
            EnsureRequiredLength(statement.AccountName, 200, "statement account");
            EnsureFileName(statement.OriginalFileName, "statement filename");
            EnsureLength(statement.CloudStoragePath, 500, "statement storage path");
            if (statement.CloudStoragePath.Contains("..", StringComparison.Ordinal)
                || Path.IsPathRooted(statement.CloudStoragePath))
                throw new InvalidDataException("A statement contains an invalid storage path.");
            EnsureLength(statement.FileType, 10, "statement file type");
            if (statement.FileHash.Length is < 32 or > 128 || statement.FileHash.Any(character => !Uri.IsHexDigit(character)))
                throw new InvalidDataException("A statement contains an invalid file hash.");
            if (statement.ImportedTransactionCount is < 0 or > MaximumTransactions)
                throw new InvalidDataException("A statement contains an invalid transaction count.");
        }

        foreach (var goal in backup.Goals)
        {
            EnsureRequiredLength(goal.Name, 200, "goal name");
            EnsureAmount(goal.TargetAmount, "goal target");
            EnsureAmount(goal.CurrentAmount, "goal current amount");
        }

        foreach (var item in backup.ScheduledItems)
        {
            EnsureAmount(item.Amount, "scheduled amount");
            EnsureLength(item.Note, 2_000, "scheduled note");
            EnsureLength(item.Frequency, 50, "scheduled frequency");
        }

        foreach (var split in backup.ExpenseSplits)
        {
            EnsureIdentifier(split.SyncId, "split identifier");
            EnsureIdentifier(split.TransactionSyncId, "split transaction identifier");
            EnsureRequiredLength(split.Title, 200, "split title");
            EnsureAmount(split.TotalAmount, "split total");
            EnsureAmount(split.UserShare, "split user share");
            EnsureLength(split.Currency, 3, "split currency");
            EnsureLength(split.SplitMethod, 50, "split method");
            EnsureCollection(split.Participants, MaximumParticipantsPerSplit, "split participants");

            foreach (var participant in split.Participants)
            {
                EnsureIdentifier(participant.SyncId, "participant identifier");
                EnsureRequiredLength(participant.Name, 200, "participant name");
                EnsureLength(participant.Contact, 320, "participant contact");
                EnsureAmount(participant.AmountOwed, "participant amount");
                EnsureLength(participant.PaymentProvider, 100, "payment provider");
                EnsureLength(participant.ExternalPaymentId, 200, "external payment identifier");
            }
        }
    }

    private static void EnsureCollection<T>(ICollection<T>? items, int maximum, string name)
    {
        if (items == null || items.Count > maximum)
            throw new InvalidDataException($"The backup contains an invalid number of {name}.");
    }

    private static void EnsureAmount(decimal amount, string name)
    {
        if (amount is < 0 or > 1_000_000_000_000m)
            throw new InvalidDataException($"The backup contains an invalid {name}.");
    }

    private static void EnsureIdentifier(string value, string name)
    {
        EnsureRequiredLength(value, 64, name);
        if (value.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
            throw new InvalidDataException($"The backup contains an invalid {name}.");
    }

    private static void EnsureFileName(string value, string name)
    {
        EnsureLength(value, 255, name);
        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || value.Contains('/')
            || value.Contains('\\'))
            throw new InvalidDataException($"The backup contains an invalid {name}.");
    }

    private static void EnsureRequiredLength(string value, int maximum, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"The backup contains an empty {name}.");
        EnsureLength(value, maximum, name);
    }

    private static void EnsureLength(string? value, int maximum, string name)
    {
        if (value == null || value.Length > maximum)
            throw new InvalidDataException($"The backup contains an invalid {name}.");
    }
}
