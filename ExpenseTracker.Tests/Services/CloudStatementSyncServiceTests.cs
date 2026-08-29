using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Tests.Services;

public class CloudStatementSyncServiceTests
{
    [Fact]
    public async Task SyncPendingAsync_UploadsLocalFileAndPersistsCloudPath()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"statement-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(filePath, "Date,Description,Amount\n2026-08-01,Coffee,-5.00");

        try
        {
            var statement = new StatementAttachment
            {
                Id = 7,
                FinancialAccountId = 3,
                OriginalFileName = "visa.csv",
                StoredFilePath = filePath,
                FileHash = "ABC123"
            };
            var accounts = new FakeFinancialAccountService(statement);
            var supabase = new FakeSupabaseService();
            var service = new CloudStatementSyncService(accounts, supabase);

            var uploaded = await service.SyncPendingAsync();

            Assert.Equal(1, uploaded);
            Assert.Equal("user-1/abc123.csv", statement.CloudStoragePath);
            Assert.Equal("ABC123", supabase.UploadedContentHash);
            Assert.Equal(1, accounts.UpdateCount);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private sealed class FakeFinancialAccountService : IFinancialAccountService
    {
        private readonly StatementAttachment _statement;

        public int UpdateCount { get; private set; }

        public FakeFinancialAccountService(StatementAttachment statement)
        {
            _statement = statement;
        }

        public Task<IReadOnlyList<StatementAttachment>> GetAllStatementsAsync() =>
            Task.FromResult<IReadOnlyList<StatementAttachment>>(new[] { _statement });

        public Task AddStatementAsync(StatementAttachment statement)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FinancialAccount>> GetAccountsAsync() =>
            Task.FromResult<IReadOnlyList<FinancialAccount>>(Array.Empty<FinancialAccount>());

        public Task AddOrUpdateAccountAsync(FinancialAccount account) => Task.CompletedTask;
        public Task DeleteAccountAsync(int accountId) => Task.CompletedTask;
        public Task ClearAllAsync() => Task.CompletedTask;
        public Task<IReadOnlyList<StatementAttachment>> GetStatementsAsync(int accountId) =>
            GetAllStatementsAsync();
        public Task<bool> HasStatementAsync(int accountId, string fileHash) => Task.FromResult(false);
    }

    private sealed class FakeSupabaseService : ISupabaseService
    {
        public AccountSession Session { get; } = new()
        {
            ProjectUrl = "https://example.supabase.co",
            PublishableKey = "publishable-key",
            UserId = "user-1",
            AccessToken = "access-token"
        };

        public string UploadedContentHash { get; private set; } = string.Empty;

        public async Task<string> UploadStatementAsync(
            Stream content,
            string objectPath,
            string contentType)
        {
            using var reader = new StreamReader(content, leaveOpen: true);
            var text = await reader.ReadToEndAsync();
            UploadedContentHash = text.Contains("Coffee", StringComparison.Ordinal) ? "ABC123" : string.Empty;
            return $"{Session.UserId}/{objectPath}";
        }

        public Task InitializeAsync() => Task.CompletedTask;
        public void SetConfiguration(string projectUrl, string publishableKey) { }
        public Task SignUpAsync(string email, string password) => Task.CompletedTask;
        public Task SignInAsync(string email, string password) => Task.CompletedTask;
        public Task SignOutAsync() => Task.CompletedTask;
        public Task UpsertBackupAsync(DataBackup backup) => Task.CompletedTask;
        public Task<DataBackup?> GetBackupAsync() => Task.FromResult<DataBackup?>(null);
        public Task<TResponse> InvokeFunctionAsync<TResponse>(string functionName, object payload) =>
            throw new NotSupportedException();
        public Task<PaymentRequestResult?> GetPaymentRequestAsync(string paymentRequestId) =>
            Task.FromResult<PaymentRequestResult?>(null);
    }
}
