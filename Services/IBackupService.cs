using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public interface IBackupService
{
    Task<string> ExportBackupAsync(string outputDirectory);
    Task<BackupImportResult> ImportBackupAsync(string backupFilePath, bool clearExistingData = true);
    Task ClearAllDataAsync();
}
