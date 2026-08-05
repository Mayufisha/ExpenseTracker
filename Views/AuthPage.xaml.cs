using ExpenseTracker.Services;

namespace ExpenseTracker.Views;

public partial class AuthPage : ContentPage
{
    private readonly IAccountService _accountService;

    public AuthPage(IAccountService accountService)
    {
        InitializeComponent();
        _accountService = accountService;
        ProjectUrlEntry.Text = _accountService.Session.ProjectUrl;
        PublishableKeyEntry.Text = _accountService.Session.PublishableKey;
        EmailEntry.Text = _accountService.Session.Email;
    }

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        try
        {
            ApplyConfiguration();
            await _accountService.RegisterAsync(EmailEntry.Text ?? string.Empty, PasswordEntry.Text ?? string.Empty);
            if (_accountService.Session.IsSignedIn)
            {
                NavigateToMain();
                return;
            }

            await DisplayAlert(
                "Confirm Your Email",
                "Your account was created. Confirm your email, then return here to log in.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Account Error", ex.Message, "OK");
        }
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        try
        {
            ApplyConfiguration();
            await _accountService.SignInAsync(EmailEntry.Text ?? string.Empty, PasswordEntry.Text ?? string.Empty);
            NavigateToMain();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Login Error", ex.Message, "OK");
        }
    }

    private void ApplyConfiguration()
    {
        _accountService.SetConfiguration(
            ProjectUrlEntry.Text ?? string.Empty,
            PublishableKeyEntry.Text ?? string.Empty);
    }

    private void NavigateToMain()
    {
        if (Application.Current is App app)
        {
            app.NavigateToMainShell();
        }
    }
}
