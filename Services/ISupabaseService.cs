using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public interface ISupabaseService
{
    AccountSession Session { get; }
    Task InitializeAsync();
    void SetConfiguration(string projectUrl, string publishableKey);
    Task SignUpAsync(string email, string password);
    Task SignInAsync(string email, string password);
    Task SignOutAsync();
    Task UpsertBackupAsync(DataBackup backup);
    Task<DataBackup?> GetBackupAsync();
    Task<string> UploadStatementAsync(Stream content, string objectPath, string contentType);
    Task<TResponse> InvokeFunctionAsync<TResponse>(string functionName, object payload);
    Task<PaymentRequestResult?> GetPaymentRequestAsync(string paymentRequestId);
}
