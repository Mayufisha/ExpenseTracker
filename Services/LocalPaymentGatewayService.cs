using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public sealed class LocalPaymentGatewayService : IPaymentGatewayService
{
    public Task<ConnectOnboardingResult> StartCardRecipientOnboardingAsync() =>
        throw HostedPaymentsRequired();

    public Task<PaymentRequestResult> CreateCardRequestAsync(
        ExpenseSplit split,
        SplitParticipant participant) => throw HostedPaymentsRequired();

    public Task<PaymentRequestResult> CreateInteracRequestAsync(
        ExpenseSplit split,
        SplitParticipant participant) => Task.FromResult(new PaymentRequestResult
        {
            Id = Guid.NewGuid().ToString(),
            Status = "pending",
            Mode = "local",
            Message = "Complete this request in your participating bank app."
        });

    public Task<PaymentRequestResult?> GetRequestAsync(string paymentRequestId) =>
        Task.FromResult<PaymentRequestResult?>(null);

    private static InvalidOperationException HostedPaymentsRequired() => new(
        "Card sandbox payments require the hosted payment backend. Local testing supports manual settlement and Interac bank handoff.");
}
