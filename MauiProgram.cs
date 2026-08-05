using ExpenseTracker.Services;
using ExpenseTracker.ViewModels;
using ExpenseTracker.Views;
using Microcharts.Maui;


namespace ExpenseTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMicrocharts();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "expenses.db3");

        builder.Services.AddSingleton(SupabaseOptions.FromAssembly());
        builder.Services.AddSingleton(new HttpClient());
        builder.Services.AddSingleton<ISupabaseService, SupabaseService>();
        builder.Services.AddSingleton<IExpenseService>(_ => new SQLiteExpenseService(dbPath));
        builder.Services.AddSingleton<IGoalService>(_ => new SQLiteGoalService(dbPath));
        builder.Services.AddSingleton<IScheduleService>(_ => new SQLiteScheduleService(dbPath));
        builder.Services.AddSingleton<IFinancialAccountService>(_ => new SQLiteFinancialAccountService(dbPath));
        builder.Services.AddSingleton<ICloudStatementSyncService, CloudStatementSyncService>();
        builder.Services.AddSingleton<IStatementImportService>(provider => new StatementImportService(
            provider.GetRequiredService<IFinancialAccountService>(),
            provider.GetRequiredService<IExpenseService>(),
            provider.GetRequiredService<ICloudStatementSyncService>(),
            Path.Combine(FileSystem.AppDataDirectory, "Statements")));
        builder.Services.AddSingleton<IBackupService, DataBackupService>();
        builder.Services.AddSingleton<IAccountService, AccountService>();

        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<TransactionsViewModel>();
        builder.Services.AddSingleton<GoalsViewModel>();
        builder.Services.AddSingleton<ScheduleViewModel>();
        builder.Services.AddSingleton<FinancialAccountsViewModel>();

        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<TransactionsPage>();
        builder.Services.AddSingleton<GoalsPage>();
        builder.Services.AddSingleton<SchedulePage>();
        builder.Services.AddSingleton<FinancialAccountsPage>();
        builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddTransient<AddEditTransactionPage>();

        return builder.Build();
    }
}
