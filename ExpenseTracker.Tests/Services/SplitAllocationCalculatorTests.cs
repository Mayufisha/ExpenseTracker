using ExpenseTracker.Services;

namespace ExpenseTracker.Tests.Services;

public class SplitAllocationCalculatorTests
{
    [Fact]
    public void CalculateEqualShares_IncludesTheCurrentUserInTheDivision()
    {
        var shares = SplitAllocationCalculator.CalculateEqualShares(90m, 2);

        Assert.Equal(new[] { 30m, 30m }, shares);
    }

    [Fact]
    public void CalculateEqualShares_RoundsDownWithoutExceedingTheTransaction()
    {
        var shares = SplitAllocationCalculator.CalculateEqualShares(10m, 2);

        Assert.Equal(new[] { 3.33m, 3.33m }, shares);
        Assert.True(shares.Sum() <= 10m);
    }
}
