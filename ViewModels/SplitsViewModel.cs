using System.Collections.ObjectModel;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.ViewModels;

public sealed class SplitsViewModel : BaseViewModel
{
    private readonly ISplitService _splitService;

    public ObservableCollection<ExpenseSplit> Splits { get; } = new();

    private decimal _totalOutstanding;
    public decimal TotalOutstanding
    {
        get => _totalOutstanding;
        private set
        {
            _totalOutstanding = value;
            OnPropertyChanged();
        }
    }

    private decimal _totalCollected;
    public decimal TotalCollected
    {
        get => _totalCollected;
        private set
        {
            _totalCollected = value;
            OnPropertyChanged();
        }
    }

    public SplitsViewModel(ISplitService splitService)
    {
        _splitService = splitService;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            Splits.Clear();
            var splits = await _splitService.GetSplitsAsync();
            foreach (var split in splits)
                Splits.Add(split);

            TotalOutstanding = splits.Sum(split => split.AmountOutstanding);
            TotalCollected = splits.Sum(split => split.AmountCollected);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
