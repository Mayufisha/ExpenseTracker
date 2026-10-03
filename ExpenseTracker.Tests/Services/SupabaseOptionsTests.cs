using ExpenseTracker.Services;

namespace ExpenseTracker.Tests.Services;

public sealed class SupabaseOptionsTests
{
    [Fact]
    public void IsConfigured_RequiresCredentialFreeHttpsOriginAndPublishableKey()
    {
        Assert.True(new SupabaseOptions
        {
            ProjectUrl = "https://money-manager.supabase.co",
            PublishableKey = "sb_publishable_test"
        }.IsConfigured);

        Assert.False(new SupabaseOptions
        {
            ProjectUrl = "http://money-manager.supabase.co",
            PublishableKey = "sb_publishable_test"
        }.IsConfigured);
        Assert.False(new SupabaseOptions
        {
            ProjectUrl = "https://money-manager.supabase.co?redirect=elsewhere",
            PublishableKey = "sb_publishable_test"
        }.IsConfigured);
        Assert.False(new SupabaseOptions
        {
            ProjectUrl = "https://money-manager.supabase.co",
            PublishableKey = "service_role_test"
        }.IsConfigured);
    }
}
