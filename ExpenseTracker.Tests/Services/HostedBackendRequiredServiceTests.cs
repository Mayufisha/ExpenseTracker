using ExpenseTracker.Services;

namespace ExpenseTracker.Tests.Services;

public sealed class HostedBackendRequiredServiceTests
{
    [Fact]
    public async Task EnsureReadyAsync_ReportsMissingMobileConfiguration()
    {
        var service = new HostedBackendRequiredService();

        var exception = await Assert.ThrowsAsync<BackendConfigurationException>(
            () => service.EnsureReadyAsync());

        Assert.Equal(HostedBackendRequiredService.ConfigurationMessage, exception.Message);
        Assert.False(service.Session.IsSignedIn);
    }

    [Fact]
    public async Task AccountOperations_CannotFallBackToLoopbackBackend()
    {
        var service = new HostedBackendRequiredService();

        await Assert.ThrowsAsync<BackendConfigurationException>(
            () => service.SignInAsync("person@example.com", "not-used"));
        Assert.Equal("Hosted backend required", service.BackendName);
    }
}
