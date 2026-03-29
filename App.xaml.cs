using ExpenseTracker.Services;
using Microsoft.Maui.Controls;

namespace ExpenseTracker;

public partial class App : Application
{
    private readonly IAccountService _accountService;

    public App(IAccountService accountService)
    {
        InitializeComponent();
        _accountService = accountService;
        ApplySavedTheme();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Page firstPage = _accountService.Session.IsSignedIn
            ? new AppShell()
            : new NavigationPage(new Views.AuthPage(_accountService));

        return new Window(firstPage);
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
