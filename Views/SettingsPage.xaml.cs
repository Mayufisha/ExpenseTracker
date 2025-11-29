using ExpenseTracker.Services;

namespace ExpenseTracker.Views;

public partial class SettingsPage : ContentPage
{
    private readonly IExpenseService _expenseService;

    public SettingsPage(IExpenseService expenseService)
    {
        InitializeComponent();
        _expenseService = expenseService;

        LoadThemePreference();
    }

    void LoadThemePreference()
    {
        try
        {
            if (Preferences.ContainsKey("AppTheme"))
            {
                var savedTheme = Preferences.Get("AppTheme", "System");
                ThemePicker.SelectedIndex = savedTheme switch
                {
                    "Light" => 1,
                    "Dark" => 2,
                    _ => 0
                };
            }
            else
            {
                ThemePicker.SelectedIndex = App.Current?.UserAppTheme switch
                {
                    AppTheme.Light => 1,
                    AppTheme.Dark => 2,
                    _ => 0
                };
            }
        }
        catch (Exception ex)
        {
            ThemePicker.SelectedIndex = 0;
        }
    }

    void OnThemeChanged(object sender, EventArgs e)
    {
        if (App.Current == null) return;

        try
        {
            switch (ThemePicker.SelectedIndex)
            {
                case 1:
                    App.Current.UserAppTheme = AppTheme.Light;
                    Preferences.Set("AppTheme", "Light");
                    break;
                case 2:
                    App.Current.UserAppTheme = AppTheme.Dark;
                    Preferences.Set("AppTheme", "Dark");
                    break;
                default:
                    App.Current.UserAppTheme = AppTheme.Unspecified;
                    Preferences.Set("AppTheme", "System");
                    break;
            }
        }
        catch (Exception ex)
        {
        }
    }

    async void OnDeleteAllClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Delete All",
            "Are you sure you want to delete all transactions? This cannot be undone.",
            "Yes", "No");

        if (!confirm) return;

        try
        {
            if (sender is Button button)
            {
                button.IsEnabled = false;
                button.Text = "Deleting...";
            }

            await _expenseService.ClearAllTransactionsAsync();
            await DisplayAlert("Done", "All transactions have been deleted.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Failed to delete transactions. Please try again.", "OK");
        }
        finally
        {
            if (sender is Button button)
            {
                button.IsEnabled = true;
                button.Text = "Delete All Transactions";
            }
        }
    }
}