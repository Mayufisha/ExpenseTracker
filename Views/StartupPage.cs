using ExpenseTracker.Services;

namespace ExpenseTracker.Views;

public sealed class StartupPage : ContentPage
{
    private readonly IAccountService _accountService;
    private readonly IBackendBootstrapper _backendBootstrapper;
    private readonly ActivityIndicator _activityIndicator;
    private readonly Label _statusLabel;
    private readonly Button _retryButton;
    private bool _started;

    public StartupPage(IAccountService accountService, IBackendBootstrapper backendBootstrapper)
    {
        _accountService = accountService;
        _backendBootstrapper = backendBootstrapper;
        _activityIndicator = new ActivityIndicator { IsRunning = true, WidthRequest = 40, HeightRequest = 40 };
        _statusLabel = new Label
        {
            Text = "Starting Money Manager securely...",
            HorizontalTextAlignment = TextAlignment.Center
        };
        _retryButton = new Button { Text = "Retry", IsVisible = false };
        _retryButton.Clicked += async (_, _) => await StartAsync();
        Content = new VerticalStackLayout
        {
            Padding = 32,
            Spacing = 16,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                _activityIndicator,
                _statusLabel,
                _retryButton
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_started) return;
        _started = true;

        await StartAsync();
    }

    private async Task StartAsync()
    {
        if (!_started) _started = true;
        _activityIndicator.IsRunning = true;
        _activityIndicator.IsVisible = true;
        _retryButton.IsVisible = false;
        _statusLabel.Text = "Starting Money Manager securely...";

        try
        {
            await _backendBootstrapper.EnsureReadyAsync();
            _statusLabel.Text = "Restoring your secure session...";
            await _accountService.InitializeAsync();
        }
        catch (Exception exception)
        {
            _activityIndicator.IsRunning = false;
            _activityIndicator.IsVisible = false;
            _statusLabel.Text = exception.Message;
            _retryButton.IsVisible = true;
            return;
        }

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
