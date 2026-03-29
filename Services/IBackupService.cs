using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public interface IBackupService
{
    Task<DataBackup> CreateBackupAsync();
    Task<BackupImportResult> ImportBackupAsync(DataBackup backup, bool clearExistingData = true);
    Task<string> ExportBackupAsync(string outputDirectory);
    Task<BackupImportResult> ImportBackupAsync(string backupFilePath, bool clearExistingData = true);
    Task ClearAllDataAsync();
}
