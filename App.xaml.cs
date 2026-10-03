using ExpenseTracker.Services;
using Microsoft.Maui.Controls;

namespace ExpenseTracker;

public partial class App : Application
{
    private readonly IAccountService _accountService;
    private readonly IBackendBootstrapper _backendBootstrapper;

    public App(IAccountService accountService, IBackendBootstrapper backendBootstrapper)
    {
        InitializeComponent();
        _accountService = accountService;
        _backendBootstrapper = backendBootstrapper;
        ApplySavedTheme();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new Views.StartupPage(_accountService, _backendBootstrapper));
        window.Destroying += (_, _) => _backendBootstrapper.Stop();
        return window;
    }

    public void NavigateToMainShell()
    {
        if (Windows.Count == 0) return;
        Windows[0].Page = new AppShell();
    }

    public void NavigateToAuth()
    {
        if (Windows.Count == 0) return;
        Windows[0].Page = new NavigationPage(new Views.AuthPage(_accountService));
    }

    private void ApplySavedTheme()
    {
        var savedTheme = Preferences.Get("AppTheme", "System");
        UserAppTheme = savedTheme switch
        {
            "Light" => AppTheme.Light,
            "Dark" => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }
}
