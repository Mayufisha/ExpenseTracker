using System.Net;

namespace ExpenseTracker.Security;

public static class RequestFirewallPolicy
{
    private const long MaximumAuthenticationBodyBytes = 4 * 1024;
    private const long MaximumBackupBodyBytes = 5 * 1024 * 1024;
    private static readonly string[] DecoyPrefixes =
    [
        "/.env", "/.git", "/actuator", "/admin", "/api/admin", "/config",
        "/graphql", "/phpmyadmin", "/server-status", "/swagger", "/wp-admin", "/wp-login"
    ];

    public static FirewallDecision Inspect(FirewallRequest request)
    {
        if (!request.IsRemoteLoopback)
            return FirewallDecision.Block(403, "non_loopback_source");
        if (!IsAllowedHost(request.Host))
            return FirewallDecision.Block(400, "invalid_host");
        if (request.HasForwardingHeaders)
            return FirewallDecision.Block(400, "unexpected_forwarding_headers");
        if (request.Path.Length is 0 or > 256 || request.QueryLength > 1024)
            return FirewallDecision.Block(414, "oversized_target");

        var normalizedPath = request.Path.ToLowerInvariant().TrimEnd('/');
        if (normalizedPath.Length == 0) normalizedPath = "/";
        if (DecoyPrefixes.Any(prefix =>
                normalizedPath.Equals(prefix, StringComparison.Ordinal)
                || normalizedPath.StartsWith($"{prefix}/", StringComparison.Ordinal)))
            return FirewallDecision.Decoy("decoy_route_probe");

        if (ContainsInvalidPathInput(request.Path))
            return FirewallDecision.Block(400, "invalid_path_encoding");
        if (request.Method is "TRACE" or "CONNECT")
            return FirewallDecision.Block(405, "dangerous_method");

        var endpointRule = GetEndpointRule(request.Method, normalizedPath);
        if (endpointRule == EndpointRule.Unknown)
            return FirewallDecision.Allow();
        if (endpointRule == EndpointRule.MethodNotAllowed)
            return FirewallDecision.Block(405, "method_not_allowed");
        if (request.QueryLength > 0)
            return FirewallDecision.Block(400, "query_not_allowed");

        var bodyLimit = normalizedPath is "/api/auth/signup" or "/api/auth/login"
            ? MaximumAuthenticationBodyBytes
            : MaximumBackupBodyBytes;
        if (request.ContentLength > bodyLimit)
            return FirewallDecision.Block(413, "body_too_large");
        if (request.Method is "POST" or "PUT"
            && request.ContentLength.GetValueOrDefault() > 0
            && !IsJsonContentType(request.ContentType))
            return FirewallDecision.Block(415, "invalid_content_type");

        return FirewallDecision.Allow();
    }

    private static EndpointRule GetEndpointRule(string method, string path) => path switch
    {
        "/health" => method == "GET" ? EndpointRule.Allowed : EndpointRule.MethodNotAllowed,
        "/api/auth/signup" or "/api/auth/login" =>
            method == "POST" ? EndpointRule.Allowed : EndpointRule.MethodNotAllowed,
        "/api/auth/session" => method == "GET" ? EndpointRule.Allowed : EndpointRule.MethodNotAllowed,
        "/api/auth/logout" => method == "POST" ? EndpointRule.Allowed : EndpointRule.MethodNotAllowed,
        "/api/backup" => method is "GET" or "PUT" ? EndpointRule.Allowed : EndpointRule.MethodNotAllowed,
        _ => EndpointRule.Unknown
    };

    private static bool IsAllowedHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

    private static bool ContainsInvalidPathInput(string path) =>
        path.Contains("..", StringComparison.Ordinal)
        || path.Contains('\\')
        || path.Contains('%')
        || path.Any(char.IsControl);

    private static bool IsJsonContentType(string? contentType) =>
        contentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true;

    private enum EndpointRule { Unknown, Allowed, MethodNotAllowed }
}

public sealed record FirewallRequest(
    string Method,
    string Path,
    string Host,
    bool IsRemoteLoopback,
    long? ContentLength,
    string? ContentType,
    bool HasForwardingHeaders,
    int QueryLength);

public sealed record FirewallDecision(bool IsAllowed, bool IsDecoy, int StatusCode, string EventType)
{
    public static FirewallDecision Allow() => new(true, false, 200, string.Empty);
    public static FirewallDecision Block(int statusCode, string eventType) =>
        new(false, false, statusCode, eventType);
    public static FirewallDecision Decoy(string eventType) => new(false, true, 404, eventType);
}
