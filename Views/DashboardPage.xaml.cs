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
            new ChartEntry((float)Math.Max(0, _viewModel.TotalIncome))
            {
                Label = "Income",
                ValueLabel = _viewModel.TotalIncome.ToString("0"),
                Color = SKColor.Parse("#16A34A"),
                TextColor = SKColors.Gray,
                ValueLabelColor = SKColors.Black
            },
            new ChartEntry((float)Math.Max(0, _viewModel.TotalExpense))
            {
                Label = "Expenses",
                ValueLabel = _viewModel.TotalExpense.ToString("0"),
                Color = SKColor.Parse("#DC2626"),
                TextColor = SKColors.Gray,
                ValueLabelColor = SKColors.Black
            },
            new ChartEntry((float)Math.Max(0, _viewModel.TotalAssets))
            {
                Label = "Assets",
                ValueLabel = _viewModel.TotalAssets.ToString("0"),
                Color = SKColor.Parse("#2563EB"),
                TextColor = SKColors.Gray,
                ValueLabelColor = SKColors.Black
            },
            new ChartEntry((float)Math.Max(0, _viewModel.TotalLiabilities))
            {
                Label = "Liabilities",
                ValueLabel = _viewModel.TotalLiabilities.ToString("0"),
                Color = SKColor.Parse("#EA580C"),
                TextColor = SKColors.Gray,
                ValueLabelColor = SKColors.Black
            }
        };

        var trendEntries = _viewModel.MonthlyNetPoints
            .Select(point => new ChartEntry((float)point.NetCashFlow)
            {
                Label = point.Label,
                ValueLabel = point.NetCashFlow.ToString("0"),
                Color = point.NetCashFlow >= 0 ? SKColor.Parse("#16A34A") : SKColor.Parse("#DC2626"),
                ValueLabelColor = SKColors.Black,
                TextColor = SKColors.Gray
            })
            .ToList();

        NetFlowChart.Chart = new LineChart
        {
            Entries = trendEntries,
            LineMode = LineMode.Straight,
            LineSize = 8,
            PointMode = PointMode.Circle,
            PointSize = 22,
            LabelOrientation = Orientation.Horizontal,
            ValueLabelOrientation = Orientation.Vertical,
            LabelTextSize = 24,
            BackgroundColor = SKColors.Transparent
        };

        CompositionChart.Chart = new DonutChart
        {
            Entries = entries,
            HoleRadius = 0.55f,
            LabelTextSize = 24,
            BackgroundColor = SKColors.Transparent
        };
    }
}
