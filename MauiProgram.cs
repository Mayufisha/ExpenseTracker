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

        var supabaseOptions = SupabaseOptions.FromAssembly();
        builder.Services.AddSingleton(supabaseOptions);
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
        if (supabaseOptions.IsConfigured)
            builder.Services.AddSingleton<IPaymentGatewayService, SupabasePaymentGatewayService>();
        else
            builder.Services.AddSingleton<IPaymentGatewayService, LocalPaymentGatewayService>();
        builder.Services.AddSingleton<ICloudStatementSyncService, CloudStatementSyncService>();
        builder.Services.AddSingleton<IStatementImportService>(provider => new StatementImportService(
            provider.GetRequiredService<IFinancialAccountService>(),
            provider.GetRequiredService<IExpenseService>(),
            provider.GetRequiredService<ICloudStatementSyncService>(),
            provider.GetRequiredService<IUserDataContext>(),
            Path.Combine(FileSystem.AppDataDirectory, "Statements")));
        builder.Services.AddSingleton<IBackupService, DataBackupService>();
        if (supabaseOptions.IsConfigured)
        {
            builder.Services.AddSingleton<IAccountService, AccountService>();
            builder.Services.AddSingleton<IBackendBootstrapper, NoOpBackendBootstrapper>();
        }
        else
        {
            builder.Services.AddSingleton<IAccountService, LocalServerAccountService>();
            builder.Services.AddSingleton<IBackendBootstrapper, LocalServerBootstrapper>();
        }

        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<TransactionsViewModel>();
        builder.Services.AddTransient<GoalsViewModel>();
        builder.Services.AddTransient<ScheduleViewModel>();
        builder.Services.AddTransient<FinancialAccountsViewModel>();
        builder.Services.AddTransient<SplitsViewModel>();

        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<TransactionsPage>();
        builder.Services.AddTransient<GoalsPage>();
        builder.Services.AddTransient<SchedulePage>();
        builder.Services.AddTransient<FinancialAccountsPage>();
        builder.Services.AddTransient<SplitsPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<AddEditTransactionPage>();
        builder.Services.AddTransient<CreateSplitPage>();

        return builder.Build();
    }
}
