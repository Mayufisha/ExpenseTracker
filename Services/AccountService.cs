using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public sealed class AccountService : IAccountService
{
    private readonly IBackupService _backupService;
    private readonly ISupabaseService _supabase;
    private readonly ICloudStatementSyncService _statementSyncService;

    public AccountSession Session => _supabase.Session;

    public AccountService(
        IBackupService backupService,
        ISupabaseService supabase,
        ICloudStatementSyncService statementSyncService)
    {
        _backupService = backupService;
        _supabase = supabase;
        _statementSyncService = statementSyncService;
    }

    public Task InitializeAsync() => _supabase.InitializeAsync();

    public void SetConfiguration(string projectUrl, string publishableKey) =>
        _supabase.SetConfiguration(projectUrl, publishableKey);

    public Task RegisterAsync(string email, string password) =>
        _supabase.SignUpAsync(email, password);

    public Task SignInAsync(string email, string password) =>
        _supabase.SignInAsync(email, password);

    public Task SignOutAsync() => _supabase.SignOutAsync();

    public async Task PushToCloudAsync()
    {
        await _statementSyncService.SyncPendingAsync();
        var backup = await _backupService.CreateBackupAsync();
        await _supabase.UpsertBackupAsync(backup);
    }

    public async Task<BackupImportResult> PullFromCloudAsync()
    {
        var backup = await _supabase.GetBackupAsync()
            ?? throw new InvalidOperationException("No cloud backup exists for this account yet.");
        return await _backupService.ImportBackupAsync(backup, clearExistingData: true);
    }
}
