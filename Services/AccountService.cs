using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public sealed class AccountService : IAccountService
{
    private readonly IBackupService _backupService;
    private readonly ISupabaseService _supabase;
    private readonly ICloudStatementSyncService _statementSyncService;
    private readonly IUserDataContext _userContext;

    public AccountSession Session => _supabase.Session;
    public string BackendName => "Supabase";

    public AccountService(
        IBackupService backupService,
        ISupabaseService supabase,
        ICloudStatementSyncService statementSyncService,
        IUserDataContext userContext)
    {
        _backupService = backupService;
        _supabase = supabase;
        _statementSyncService = statementSyncService;
        _userContext = userContext;
    }

    public async Task InitializeAsync()
    {
        await _supabase.InitializeAsync();
        SetUserContextFromSession();
    }

    public async Task RegisterAsync(string email, string password)
    {
        await _supabase.SignUpAsync(email, password);
        SetUserContextFromSession();
    }

    public async Task SignInAsync(string email, string password)
    {
        await _supabase.SignInAsync(email, password);
        SetUserContextFromSession();
    }

    public async Task SignOutAsync()
    {
        await _supabase.SignOutAsync();
        _userContext.Clear();
    }

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

    private void SetUserContextFromSession()
    {
        if (_supabase.Session.IsSignedIn)
            _userContext.SetCurrentUser(_supabase.Session.UserId);
        else
            _userContext.Clear();
    }
}
