using System.Collections.ObjectModel;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.ViewModels;

public sealed class SplitsViewModel : BaseViewModel
{
    private readonly ISplitService _splitService;

    public ObservableCollection<ExpenseSplit> Splits { get; } = new();

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        private set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
    }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

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
        ErrorMessage = string.Empty;

        try
        {
            var splits = await _splitService.GetSplitsAsync();
            Splits.Clear();
            foreach (var split in splits)
                Splits.Add(split);

            TotalOutstanding = splits.Sum(split => split.AmountOutstanding);
            TotalCollected = splits.Sum(split => split.AmountCollected);
        }
        catch (Exception)
        {
            ErrorMessage = "Could not load SplitFare. Your saved splits have not been changed. Try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
