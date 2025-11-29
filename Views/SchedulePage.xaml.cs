using Microsoft.Maui.Controls;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class SchedulePage : ContentPage
{
    private readonly ScheduleViewModel _viewModel;

    public SchedulePage(ScheduleViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }
}
