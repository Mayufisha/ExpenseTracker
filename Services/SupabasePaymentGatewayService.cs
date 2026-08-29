using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public sealed class SupabasePaymentGatewayService : IPaymentGatewayService
{
    private readonly ISupabaseService _supabase;

    public SupabasePaymentGatewayService(ISupabaseService supabase)
    {
        _supabase = supabase;
    }

    public async Task<ConnectOnboardingResult> StartCardRecipientOnboardingAsync()
    {
        var result = await _supabase.InvokeFunctionAsync<ConnectOnboardingResult>(
            "create-connect-account",
            new { });
        if (!Uri.TryCreate(result.OnboardingUrl, UriKind.Absolute, out var onboardingUri)
            || onboardingUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("The payment provider returned an invalid onboarding URL.");
        }

        await Browser.Default.OpenAsync(onboardingUri, BrowserLaunchMode.SystemPreferred);
        return result;
    }

    public async Task<PaymentRequestResult> CreateCardRequestAsync(
        ExpenseSplit split,
        SplitParticipant participant)
    {
        if (participant.IsPaid)
            throw new InvalidOperationException("This share is already marked paid.");

        if (participant.PaymentProvider == "stripe"
            && Guid.TryParse(participant.ExternalPaymentId, out _))
        {
            var existing = await _supabase.GetPaymentRequestAsync(participant.ExternalPaymentId);
            if (existing is { Status: "pending" or "processing" }
                && !string.IsNullOrWhiteSpace(existing.CheckoutUrl))
            {
                return existing;
            }
        }

        return await _supabase.InvokeFunctionAsync<PaymentRequestResult>(
            "create-card-payment",
            new
            {
                splitSyncId = split.SyncId,
                participantSyncId = participant.SyncId,
                participantName = participant.Name,
                amount = participant.AmountOwed,
                currency = split.Currency,
                idempotencyKey = Guid.NewGuid(),
                description = $"{split.Title} - {participant.Name}'s share"
            });
    }

    public async Task<PaymentRequestResult> CreateInteracRequestAsync(
        ExpenseSplit split,
        SplitParticipant participant)
    {
        if (participant.IsPaid)
            throw new InvalidOperationException("This share is already marked paid.");

        if (participant.PaymentProvider == "interac"
            && Guid.TryParse(participant.ExternalPaymentId, out _))
        {
            var existing = await _supabase.GetPaymentRequestAsync(participant.ExternalPaymentId);
            if (existing is { Status: "pending" or "processing" }) return existing;
        }

        return await _supabase.InvokeFunctionAsync<PaymentRequestResult>(
            "create-interac-request",
            new
            {
                splitSyncId = split.SyncId,
                participantSyncId = participant.SyncId,
                participantName = participant.Name,
                amount = participant.AmountOwed,
                idempotencyKey = Guid.NewGuid()
            });
    }

    public Task<PaymentRequestResult?> GetRequestAsync(string paymentRequestId) =>
        _supabase.GetPaymentRequestAsync(paymentRequestId);
}
