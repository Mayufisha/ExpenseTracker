using System.Reflection;

namespace ExpenseTracker.Services;

public sealed class SupabaseOptions
{
    public string ProjectUrl { get; init; } = string.Empty;
    public string PublishableKey { get; init; } = string.Empty;
    public bool IsConfigured => IsValidProjectUrl(ProjectUrl) && IsValidPublishableKey(PublishableKey);

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

    private static bool IsValidProjectUrl(string projectUrl)
    {
        return Uri.TryCreate(projectUrl, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && string.IsNullOrEmpty(uri.UserInfo)
            && uri.AbsolutePath == "/"
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment);
    }

    private static bool IsValidPublishableKey(string publishableKey)
    {
        return !string.IsNullOrWhiteSpace(publishableKey)
            && publishableKey.Length <= 4096
            && publishableKey.All(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-')
            && !publishableKey.Contains("service_role", StringComparison.OrdinalIgnoreCase)
            && !publishableKey.Contains("secret", StringComparison.OrdinalIgnoreCase);
    }
}
