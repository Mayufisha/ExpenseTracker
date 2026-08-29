using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public sealed class PaymentRequestService : IPaymentRequestService
{
    private const string ETransferRecipientKey = "Payments.ETransferRecipient";
    private const string OnlineBankingUrlKey = "Payments.OnlineBankingUrl";
    private const string InteracInformationUrl =
        "https://www.interac.ca/en/payments/personal/send-receive-money-with-interac-e-transfer/";

    public string ETransferRecipient => Preferences.Get(ETransferRecipientKey, string.Empty);
    public string OnlineBankingUrl => Preferences.Get(OnlineBankingUrlKey, string.Empty);

    public void SavePreferences(string eTransferRecipient, string onlineBankingUrl)
    {
        var recipient = eTransferRecipient?.Trim() ?? string.Empty;
        var bankUrl = onlineBankingUrl?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(bankUrl)
            && (!Uri.TryCreate(bankUrl, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Online banking must be a valid HTTPS URL.");
        }

        Preferences.Set(ETransferRecipientKey, recipient);
        Preferences.Set(OnlineBankingUrlKey, bankUrl);
    }

    public Task ShareCardRequestAsync(
        ExpenseSplit split,
        SplitParticipant participant,
        string checkoutUrl)
    {
        if (!Uri.TryCreate(checkoutUrl, UriKind.Absolute, out var checkoutUri)
            || checkoutUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("The card checkout URL is invalid.");
        }

        return Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = $"Card payment request for {split.Title}",
            Text =
                $"Hi {participant.Name}, your share of {split.Title} is " +
                $"{split.Currency} {participant.AmountOwed:F2}. Pay securely by Visa or Mastercard: " +
                checkoutUri
        });
    }

    public async Task OpenInteracHandoffAsync(ExpenseSplit split, SplitParticipant participant)
    {
        if (string.IsNullOrWhiteSpace(participant.Contact))
            throw new InvalidOperationException("Add the participant's email or mobile number before requesting an e-Transfer.");
        if (string.IsNullOrWhiteSpace(ETransferRecipient))
            throw new InvalidOperationException("Set your e-Transfer deposit email or mobile number in Settings first.");

        var details =
            $"Interac Request Money\nTo: {participant.Name} ({participant.Contact})\n" +
            $"Amount: CAD {participant.AmountOwed:F2}\nMessage: {split.Title}\n" +
            $"Deposit recipient: {ETransferRecipient}";
        await Clipboard.Default.SetTextAsync(details);

        var url = Uri.TryCreate(OnlineBankingUrl, UriKind.Absolute, out var bankUri)
            ? bankUri
            : new Uri(InteracInformationUrl);
        await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
    }
}
