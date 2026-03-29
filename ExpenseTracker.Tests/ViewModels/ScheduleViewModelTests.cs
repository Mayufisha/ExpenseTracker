using ExpenseTracker.Models;
using ExpenseTracker.Tests.TestDoubles;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Tests.ViewModels;

public class ScheduleViewModelTests
{
    [Fact]
    public async Task SelectedMonthFilter_ThisMonth_OnlyReturnsCurrentMonth()
    {
        var today = DateTime.Today;
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var previousMonth = startOfMonth.AddDays(-1);

        var vm = new ScheduleViewModel(new FakeScheduleService(new[]
        {
            NewScheduled(startOfMonth.AddDays(2)),
            NewScheduled(today),
            NewScheduled(previousMonth)
        }));

        await vm.LoadAsync();
        vm.SelectedMonthFilter = vm.MonthFilters.First(f => f.Key == startOfMonth.ToString("yyyy-MM"));

        Assert.Equal(2, vm.ScheduledItems.Count);
        Assert.All(vm.ScheduledItems, item => Assert.True(item.ScheduledDate >= startOfMonth));
    }

    private static ScheduledTransaction NewScheduled(DateTime date)
    {
        return new ScheduledTransaction
        {
            Amount = 50m,
            ScheduledDate = date,
            Note = "Test"
        };
    }
}
