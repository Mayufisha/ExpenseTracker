namespace ExpenseTracker.Services;

public static class ReleaseFeatures
{
    public static bool TestPaymentsEnabled
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }
}
