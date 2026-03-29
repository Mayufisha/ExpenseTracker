using ExpenseTracker.Services;

namespace ExpenseTracker.Views;

public partial class AuthPage : ContentPage
{
    private readonly IAccountService _accountService;

    public AuthPage(IAccountService accountService)
    {
        InitializeComponent();
        _accountService = accountService;
        ServerUrlEntry.Text = _accountService.Session.ServerUrl;
        EmailEntry.Text = _accountService.Session.Email;
    }

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        try
        {
            ApplyServerUrl();
            await _accountService.RegisterAsync(EmailEntry.Text ?? string.Empty, PasswordEntry.Text ?? string.Empty);
            await _accountService.SignInAsync(EmailEntry.Text ?? string.Empty, PasswordEntry.Text ?? string.Empty);
            NavigateToMain();
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
            ApplyServerUrl();
            await _accountService.SignInAsync(EmailEntry.Text ?? string.Empty, PasswordEntry.Text ?? string.Empty);
            NavigateToMain();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Login Error", ex.Message, "OK");
        }
    }

    private void OnGuestClicked(object sender, EventArgs e)
    {
        NavigateToMain();
    }

    private void ApplyServerUrl()
    {
        _accountService.SetServerUrl(ServerUrlEntry.Text ?? string.Empty);
    }

    private void NavigateToMain()
    {
        if (Application.Current is App app)
        {
            app.NavigateToMainShell();
        }
    }
}
