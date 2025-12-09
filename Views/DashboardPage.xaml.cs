using System;
using ExpenseTracker.Drawables;
using ExpenseTracker.ViewModels;
using Microsoft.Maui.Controls;

namespace ExpenseTracker.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;
    private readonly BarChartDrawable _chartDrawable = new();

    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel;

        ChartView.Drawable = _chartDrawable;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.LoadAsync();

        _chartDrawable.Income = (float)_viewModel.TotalIncome;
        _chartDrawable.Expense = (float)_viewModel.TotalExpense; 
        _chartDrawable.Balance = (float)_viewModel.Balance;

        ChartView.Invalidate();
    }
}
