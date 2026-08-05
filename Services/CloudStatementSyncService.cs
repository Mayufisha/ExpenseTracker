namespace ExpenseTracker.Services;

public sealed class CloudStatementSyncService : ICloudStatementSyncService
{
    private readonly IFinancialAccountService _accountService;
    private readonly ISupabaseService _supabase;

    public CloudStatementSyncService(
        IFinancialAccountService accountService,
        ISupabaseService supabase)
    {
        _accountService = accountService;
        _supabase = supabase;
    }

    public async Task<int> SyncPendingAsync()
    {
        if (!_supabase.Session.IsSignedIn) return 0;

        var statements = await _accountService.GetAllStatementsAsync();
        var uploaded = 0;

        foreach (var statement in statements.Where(statement =>
                     string.IsNullOrWhiteSpace(statement.CloudStoragePath)
                     && !string.IsNullOrWhiteSpace(statement.StoredFilePath)
                     && File.Exists(statement.StoredFilePath)))
        {
            await using var stream = File.OpenRead(statement.StoredFilePath);
            var extension = Path.GetExtension(statement.OriginalFileName).ToLowerInvariant();
            var objectPath = $"{statement.FileHash.ToLowerInvariant()}{extension}";
            statement.CloudStoragePath = await _supabase.UploadStatementAsync(
                stream,
                objectPath,
                GetContentType(extension));
            await _accountService.AddStatementAsync(statement);
            uploaded++;
        }

        return uploaded;
    }

    private static string GetContentType(string extension) => extension switch
    {
        ".pdf" => "application/pdf",
        _ => "text/csv"
    };
}
