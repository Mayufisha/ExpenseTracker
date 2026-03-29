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
        var isDark = RequestedTheme == AppTheme.Dark;
        var chartLabelColor = isDark ? SKColors.LightGray : SKColors.Gray;
        var chartValueColor = isDark ? SKColors.White : SKColors.Black;

        var entries = new[]
        {
            new ChartEntry((float)Math.Max(0, _viewModel.TotalIncome))
            {
                Label = "Income",
                ValueLabel = _viewModel.TotalIncome.ToString("0"),
                Color = SKColor.Parse("#16A34A"),
                TextColor = chartLabelColor,
                ValueLabelColor = chartValueColor
            },
            new ChartEntry((float)Math.Max(0, _viewModel.TotalExpense))
            {
                Label = "Expenses",
                ValueLabel = _viewModel.TotalExpense.ToString("0"),
                Color = SKColor.Parse("#DC2626"),
                TextColor = chartLabelColor,
                ValueLabelColor = chartValueColor
            },
            new ChartEntry((float)Math.Max(0, _viewModel.TotalAssets))
            {
                Label = "Assets",
                ValueLabel = _viewModel.TotalAssets.ToString("0"),
                Color = SKColor.Parse("#2563EB"),
                TextColor = chartLabelColor,
                ValueLabelColor = chartValueColor
            },
            new ChartEntry((float)Math.Max(0, _viewModel.TotalLiabilities))
            {
                Label = "Liabilities",
                ValueLabel = _viewModel.TotalLiabilities.ToString("0"),
                Color = SKColor.Parse("#EA580C"),
                TextColor = chartLabelColor,
                ValueLabelColor = chartValueColor
            }
        };

        var trendEntries = _viewModel.MonthlyNetPoints
            .Select(point => new ChartEntry((float)point.NetCashFlow)
            {
                Label = point.Label,
                ValueLabel = point.NetCashFlow.ToString("0"),
                Color = point.NetCashFlow >= 0 ? SKColor.Parse("#16A34A") : SKColor.Parse("#DC2626"),
                ValueLabelColor = chartValueColor,
                TextColor = chartLabelColor
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
