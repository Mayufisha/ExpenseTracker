using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public interface IPaymentRequestService
{
    string ETransferRecipient { get; }
    string OnlineBankingUrl { get; }
    void SavePreferences(string eTransferRecipient, string onlineBankingUrl);
    Task ShareCardRequestAsync(
        ExpenseSplit split,
        SplitParticipant participant,
        string checkoutUrl);
    Task OpenInteracHandoffAsync(ExpenseSplit split, SplitParticipant participant);
}
