namespace ExpenseTracker.Services;

public interface ICloudStatementSyncService
{
    Task<int> SyncPendingAsync();
}
