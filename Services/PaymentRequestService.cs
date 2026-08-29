using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public sealed class PaymentRequestService : IPaymentRequestService
{
    public Task ShareRequestAsync(ExpenseSplit split, SplitParticipant participant)
    {
        var message =
            $"Hi {participant.Name}, your share of {split.Title} is " +
            $"{split.Currency} {participant.AmountOwed:F2}. Please send it when you can.";

        return Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = $"Payment request for {split.Title}",
            Text = message
        });
    }
}
