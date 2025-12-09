using ExpenseTracker.ViewModels;
using Microcharts;
using SkiaSharp;

namespace ExpenseTracker.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;

    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();

        var entries = new[]
        {
            new ChartEntry((float)_viewModel.TotalIncome)
            {
                Label = "Income",
                ValueLabel = _viewModel.TotalIncome.ToString("0"),
                Color = SKColor.Parse("#4CAF50"),
                TextColor = SKColors.Gray,
                ValueLabelColor = SKColors.Black
            },
            new ChartEntry((float)_viewModel.TotalExpense)
            {
                Label = "Expenses",
                ValueLabel = _viewModel.TotalExpense.ToString("0"),
                Color = SKColor.Parse("#F44336"),
                TextColor = SKColors.Gray,
                ValueLabelColor = SKColors.Black
            },
            new ChartEntry((float)_viewModel.Balance)
            {
                Label = "Balance",
                ValueLabel = _viewModel.Balance.ToString("0"),
                Color = SKColor.Parse("#2196F3"),
                TextColor = SKColors.Gray,
                ValueLabelColor = SKColors.Black
            }
        };

        DashboardChart.Chart = new BarChart
        {
            Entries = entries,
            //Orientation = Orientation.Vertical,         
            LabelOrientation = Orientation.Horizontal,   
            ValueLabelOrientation = Orientation.Vertical,
            LabelTextSize = 24,
            BackgroundColor = SKColors.Transparent,
            Margin = 20
        };
    }
}
