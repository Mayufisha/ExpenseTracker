using SQLite;

namespace ExpenseTracker.Models;

public class StatementAttachment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int FinancialAccountId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFilePath { get; set; } = string.Empty;
    public string CloudStoragePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public DateTime AttachedAt { get; set; } = DateTime.UtcNow;
    public int ImportedTransactionCount { get; set; }

    [Ignore]
    public bool IsCloudSynced => !string.IsNullOrWhiteSpace(CloudStoragePath);
}
