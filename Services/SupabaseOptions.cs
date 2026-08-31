using System.Reflection;

namespace ExpenseTracker.Services;

public sealed class SupabaseOptions
{
    public string ProjectUrl { get; init; } = string.Empty;
    public string PublishableKey { get; init; } = string.Empty;
    public bool IsConfigured => Uri.TryCreate(ProjectUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrWhiteSpace(PublishableKey);

    public static SupabaseOptions FromAssembly()
    {
        var metadata = typeof(SupabaseOptions).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value ?? string.Empty);

        return new SupabaseOptions
        {
            ProjectUrl = metadata.GetValueOrDefault("SupabaseUrl", string.Empty),
            PublishableKey = metadata.GetValueOrDefault("SupabasePublishableKey", string.Empty)
        };
    }
}
