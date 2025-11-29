using ExpenseTracker.Services;
using ExpenseTracker.ViewModels;
using ExpenseTracker.Views;

namespace ExpenseTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "expenses.db3");
        builder.Services.AddSingleton<IExpenseService>(_ => new SQLiteExpenseService(dbPath));

        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<TransactionsViewModel>();

        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<TransactionsPage>();
        builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddTransient<AddEditTransactionPage>();

        builder.Services.AddSingleton<IExpenseService>(_ => new SQLiteExpenseService(dbPath));
        builder.Services.AddSingleton<IGoalService>(_ => new SQLiteGoalService(dbPath));

        builder.Services.AddSingleton<GoalsViewModel>();
        builder.Services.AddSingleton<GoalsPage>();

        builder.Services.AddSingleton<ScheduleViewModel>();
        builder.Services.AddSingleton<SchedulePage>();


        return builder.Build();
    }
}
