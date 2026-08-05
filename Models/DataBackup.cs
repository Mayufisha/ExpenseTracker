namespace ExpenseTracker.Models;

public class DataBackup
{
    public int Version { get; set; } = 2;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<TransactionBackupItem> Transactions { get; set; } = new();
    public List<FinancialAccountBackupItem> FinancialAccounts { get; set; } = new();
    public List<StatementBackupItem> Statements { get; set; } = new();
    public List<Goal> Goals { get; set; } = new();
    public List<ScheduledTransaction> ScheduledItems { get; set; } = new();
}

public class TransactionBackupItem
{
    public decimal Amount { get; set; }
    public string Type { get; set; } = TransactionType.Expense.ToString();
    public DateTime Date { get; set; }
    public string Note { get; set; } = string.Empty;
    public string CategoryName { get; set; } = "Other";
    public string InstitutionName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string StatementFileName { get; set; } = string.Empty;
}

public class FinancialAccountBackupItem
{
    public string InstitutionName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = "Bank Account";
    public string LastFour { get; set; } = string.Empty;
}

public class StatementBackupItem
{
    public string InstitutionName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string CloudStoragePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public DateTime AttachedAt { get; set; }
    public int ImportedTransactionCount { get; set; }
}

public class BackupImportResult
{
    public int ImportedTransactions { get; set; }
    public int ImportedGoals { get; set; }
    public int ImportedScheduledItems { get; set; }
    public int ImportedStatements { get; set; }
}
