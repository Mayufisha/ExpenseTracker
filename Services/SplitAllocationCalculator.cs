namespace ExpenseTracker.Services;

public static class SplitAllocationCalculator
{
    public static IReadOnlyList<decimal> CalculateEqualShares(decimal total, int participantCount)
    {
        if (total <= 0)
            throw new InvalidOperationException("The transaction total must be greater than zero.");
        if (participantCount <= 0)
            throw new InvalidOperationException("Add at least one person to this split.");

        // Round down so participant shares can never exceed the original transaction.
        var share = decimal.Floor(total / (participantCount + 1) * 100) / 100;
        if (share <= 0)
            throw new InvalidOperationException("The transaction is too small to split equally.");

        return Enumerable.Repeat(share, participantCount).ToList();
    }
}
