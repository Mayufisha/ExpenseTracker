using Microsoft.Maui.Controls;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class GoalsPage : ContentPage
{
    private readonly GoalsViewModel _viewModel;

    public GoalsPage(GoalsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
