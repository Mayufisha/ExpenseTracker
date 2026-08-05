using ExpenseTracker.Services;

namespace ExpenseTracker.Views;

public sealed class StartupPage : ContentPage
{
    private readonly IAccountService _accountService;
    private bool _started;

    public StartupPage(IAccountService accountService)
    {
        _accountService = accountService;
        Content = new VerticalStackLayout
        {
            Padding = 32,
            Spacing = 16,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new ActivityIndicator { IsRunning = true, WidthRequest = 40, HeightRequest = 40 },
                new Label
                {
                    Text = "Restoring your secure session...",
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_started) return;
        _started = true;

        await _accountService.InitializeAsync();
        if (Application.Current is not App app) return;

        if (_accountService.Session.IsSignedIn)
        {
            app.NavigateToMainShell();
        }
        else
        {
            app.NavigateToAuth();
        }
    }
}
