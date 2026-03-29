using ExpenseTracker.Services;

namespace ExpenseTracker.Views;

public partial class SettingsPage : ContentPage
{
    private readonly IBackupService _backupService;

    public SettingsPage(IBackupService backupService)
    {
        InitializeComponent();
        _backupService = backupService;

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
        catch (Exception)
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
        catch (Exception)
        {
        }
    }

    async void OnDeleteAllClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Delete All",
            "Are you sure you want to delete all transactions, goals, and scheduled items? This cannot be undone.",
            "Yes", "No");

        if (!confirm) return;

        try
        {
            if (sender is Button button)
            {
                button.IsEnabled = false;
                button.Text = "Deleting...";
            }

            await _backupService.ClearAllDataAsync();
            await DisplayAlert("Done", "All data has been deleted.", "OK");
        }
        catch (Exception)
        {
            await DisplayAlert("Error", "Failed to delete data. Please try again.", "OK");
        }
        finally
        {
            if (sender is Button button)
            {
                button.IsEnabled = true;
                button.Text = "Delete All Data";
            }
        }
    }

    async void OnExportBackupClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button button)
            {
                button.IsEnabled = false;
                button.Text = "Exporting...";
            }

            var path = await _backupService.ExportBackupAsync(FileSystem.CacheDirectory);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "ExpenseTracker Backup",
                File = new ShareFile(path)
            });
        }
        catch (Exception)
        {
            await DisplayAlert("Error", "Failed to export backup.", "OK");
        }
        finally
        {
            if (sender is Button button)
            {
                button.IsEnabled = true;
                button.Text = "Export Backup";
            }
        }
    }

    async void OnImportBackupClicked(object sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose backup file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".json" } },
                    { DevicePlatform.Android, new[] { "application/json", "text/json" } },
                    { DevicePlatform.iOS, new[] { "public.json" } },
                    { DevicePlatform.MacCatalyst, new[] { "public.json" } }
                })
            });

            if (file == null)
            {
                return;
            }

            var confirm = await DisplayAlert(
                "Import Backup",
                "Importing a backup will replace your existing transactions, goals, and scheduled items. Continue?",
                "Import",
                "Cancel");

            if (!confirm)
            {
                return;
            }

            if (sender is Button button)
            {
                button.IsEnabled = false;
                button.Text = "Importing...";
            }

            var result = await _backupService.ImportBackupAsync(file.FullPath, clearExistingData: true);
            await DisplayAlert(
                "Import Complete",
                $"Transactions: {result.ImportedTransactions}\nGoals: {result.ImportedGoals}\nScheduled items: {result.ImportedScheduledItems}",
                "OK");
        }
        catch (Exception)
        {
            await DisplayAlert("Error", "Failed to import backup file.", "OK");
        }
        finally
        {
            if (sender is Button button)
            {
                button.IsEnabled = true;
                button.Text = "Import Backup";
            }
        }
    }
}
