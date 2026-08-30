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
        builder.Services.AddSingleton(new LocalServerOptions());
        builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(10) });
        builder.Services.AddSingleton<IUserDataContext, UserDataContext>();
        builder.Services.AddSingleton<ISupabaseService, SupabaseService>();
        builder.Services.AddSingleton<IExpenseService>(provider => new SQLiteExpenseService(
            dbPath,
            provider.GetRequiredService<IUserDataContext>()));
        builder.Services.AddSingleton<IGoalService>(provider => new SQLiteGoalService(
            dbPath,
            provider.GetRequiredService<IUserDataContext>()));
        builder.Services.AddSingleton<IScheduleService>(provider => new SQLiteScheduleService(
            dbPath,
            provider.GetRequiredService<IUserDataContext>()));
        builder.Services.AddSingleton<IFinancialAccountService>(provider => new SQLiteFinancialAccountService(
            dbPath,
            provider.GetRequiredService<IUserDataContext>()));
        builder.Services.AddSingleton<ISplitService>(provider => new SQLiteSplitService(
            dbPath,
            provider.GetRequiredService<IUserDataContext>()));
        builder.Services.AddSingleton<IPaymentRequestService, PaymentRequestService>();
        builder.Services.AddSingleton<IPaymentGatewayService, LocalPaymentGatewayService>();
        builder.Services.AddSingleton<ICloudStatementSyncService, CloudStatementSyncService>();
        builder.Services.AddSingleton<IStatementImportService>(provider => new StatementImportService(
            provider.GetRequiredService<IFinancialAccountService>(),
            provider.GetRequiredService<IExpenseService>(),
            provider.GetRequiredService<ICloudStatementSyncService>(),
            provider.GetRequiredService<IUserDataContext>(),
            Path.Combine(FileSystem.AppDataDirectory, "Statements")));
        builder.Services.AddSingleton<IBackupService, DataBackupService>();
        builder.Services.AddSingleton<IAccountService, LocalServerAccountService>();

        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<TransactionsViewModel>();
        builder.Services.AddSingleton<GoalsViewModel>();
        builder.Services.AddSingleton<ScheduleViewModel>();
        builder.Services.AddSingleton<FinancialAccountsViewModel>();
        builder.Services.AddSingleton<SplitsViewModel>();

        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<TransactionsPage>();
        builder.Services.AddSingleton<GoalsPage>();
        builder.Services.AddSingleton<SchedulePage>();
        builder.Services.AddSingleton<FinancialAccountsPage>();
        builder.Services.AddSingleton<SplitsPage>();
        builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddTransient<AddEditTransactionPage>();
        builder.Services.AddTransient<CreateSplitPage>();

        return builder.Build();
    }
}
