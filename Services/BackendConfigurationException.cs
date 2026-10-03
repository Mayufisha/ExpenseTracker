namespace ExpenseTracker.Services;

public sealed class BackendConfigurationException : InvalidOperationException
{
    public BackendConfigurationException(string message) : base(message)
    {
    }
}
