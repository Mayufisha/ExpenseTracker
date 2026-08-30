using ExpenseTracker.Services;

namespace ExpenseTracker.Views;

public partial class AuthPage : ContentPage
{
    private readonly IAccountService _accountService;

    public AuthPage(IAccountService accountService)
    {
        InitializeComponent();
        _accountService = accountService;
        EmailEntry.Text = _accountService.Session.Email;
    }

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        try
        {
            await _accountService.RegisterAsync(EmailEntry.Text ?? string.Empty, PasswordEntry.Text ?? string.Empty);
            if (_accountService.Session.IsSignedIn)
            {
                NavigateToMain();
                return;
            }

            await DisplayAlert("Account Error", "The account was created without an active session.", "OK");
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
            await _accountService.SignInAsync(EmailEntry.Text ?? string.Empty, PasswordEntry.Text ?? string.Empty);
            NavigateToMain();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Login Error", ex.Message, "OK");
        }
    }

    private void NavigateToMain()
    {
        if (Application.Current is App app)
        {
            app.NavigateToMainShell();
        }
    }
}
