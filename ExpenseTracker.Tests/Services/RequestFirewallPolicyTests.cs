using ExpenseTracker.Security;

namespace ExpenseTracker.Tests.Services;

public class RequestFirewallPolicyTests
{
    [Fact]
    public void Inspect_AllowsExpectedLoopbackRequest()
    {
        var decision = RequestFirewallPolicy.Inspect(Request("POST", "/api/auth/login", contentLength: 120));

        Assert.True(decision.IsAllowed);
    }

    [Theory]
    [InlineData("/.env")]
    [InlineData("/wp-admin/setup.php")]
    [InlineData("/api/admin/users")]
    public void Inspect_DetectsDecoyRoutes(string path)
    {
        var decision = RequestFirewallPolicy.Inspect(Request("GET", path));

        Assert.False(decision.IsAllowed);
        Assert.True(decision.IsDecoy);
        Assert.Equal(404, decision.StatusCode);
    }

    [Fact]
    public void Inspect_BlocksNonLoopbackAndForwardedRequests()
    {
        Assert.Equal(403, RequestFirewallPolicy.Inspect(Request("GET", "/health") with
        {
            IsRemoteLoopback = false
        }).StatusCode);
        Assert.Equal(400, RequestFirewallPolicy.Inspect(Request("GET", "/health") with
        {
            HasForwardingHeaders = true
        }).StatusCode);
    }

    [Theory]
    [InlineData("TRACE", "/health", 405)]
    [InlineData("POST", "/health", 405)]
    [InlineData("POST", "/api/auth/login", 415)]
    [InlineData("POST", "/api/auth/login?next=x", 400)]
    public void Inspect_RejectsInvalidRequestShapes(string method, string path, int expectedStatus)
    {
        var queryIndex = path.IndexOf('?');
        var queryLength = queryIndex >= 0 ? path.Length - queryIndex : 0;
        if (queryIndex >= 0) path = path[..queryIndex];
        var request = Request(method, path, contentLength: method == "POST" ? 10 : 0) with
        {
            ContentType = expectedStatus == 415 ? "text/plain" : "application/json",
            QueryLength = queryLength
        };

        Assert.Equal(expectedStatus, RequestFirewallPolicy.Inspect(request).StatusCode);
    }

    [Fact]
    public void Inspect_RejectsOversizedAuthenticationBody()
    {
        var decision = RequestFirewallPolicy.Inspect(Request(
            "POST",
            "/api/auth/signup",
            contentLength: 4097));

        Assert.Equal(413, decision.StatusCode);
    }

    private static FirewallRequest Request(string method, string path, long? contentLength = 0) => new(
        method,
        path,
        "127.0.0.1",
        true,
        contentLength,
        "application/json",
        false,
        0);
}
