using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public interface IPaymentGatewayService
{
    Task<ConnectOnboardingResult> StartCardRecipientOnboardingAsync();
    Task<PaymentRequestResult> CreateCardRequestAsync(ExpenseSplit split, SplitParticipant participant);
    Task<PaymentRequestResult> CreateInteracRequestAsync(ExpenseSplit split, SplitParticipant participant);
    Task<PaymentRequestResult?> GetRequestAsync(string paymentRequestId);
}
