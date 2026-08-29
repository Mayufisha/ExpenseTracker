using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public interface IPaymentRequestService
{
    Task ShareRequestAsync(ExpenseSplit split, SplitParticipant participant);
}
