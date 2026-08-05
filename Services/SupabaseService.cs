using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public sealed class SupabaseService : ISupabaseService
{
    private const string ProjectUrlKey = "Supabase.ProjectUrl";
    private const string PublishableKeyKey = "Supabase.PublishableKey";
    private const string EmailKey = "Supabase.Email";
    private const string AccessTokenKey = "Supabase.AccessToken";
    private const string RefreshTokenKey = "Supabase.RefreshToken";
    private const string UserIdKey = "Supabase.UserId";
    private const string ExpiresAtKey = "Supabase.ExpiresAtUtc";

    private readonly HttpClient _httpClient;
    private bool _initialized;

    public AccountSession Session { get; } = new();

    public SupabaseService(SupabaseOptions options, HttpClient httpClient)
    {
        _httpClient = httpClient;
        Session.ProjectUrl = Preferences.Get(ProjectUrlKey, options.ProjectUrl);
        Session.PublishableKey = Preferences.Get(PublishableKeyKey, options.PublishableKey);
        Session.Email = Preferences.Get(EmailKey, string.Empty);
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        Session.AccessToken = await GetSecureValueAsync(AccessTokenKey);
        Session.RefreshToken = await GetSecureValueAsync(RefreshTokenKey);
        Session.UserId = await GetSecureValueAsync(UserIdKey);

        var expiresAtValue = await GetSecureValueAsync(ExpiresAtKey);
        if (long.TryParse(expiresAtValue, out var expiresAtUnix))
        {
            Session.ExpiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix);
        }

        if (!Session.IsConfigured || string.IsNullOrWhiteSpace(Session.RefreshToken))
        {
            await ClearSessionAsync();
            return;
        }

        try
        {
            await RefreshSessionAsync();
        }
        catch
        {
            await ClearSessionAsync();
        }
    }

    public void SetConfiguration(string projectUrl, string publishableKey)
    {
        var normalizedUrl = projectUrl?.Trim().TrimEnd('/') ?? string.Empty;
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Enter a valid HTTPS Supabase project URL.");
        }

        if (string.IsNullOrWhiteSpace(publishableKey))
        {
            throw new InvalidOperationException("Enter the Supabase publishable or anon key.");
        }

        var configurationChanged =
            !Session.ProjectUrl.Equals(normalizedUrl, StringComparison.OrdinalIgnoreCase)
            || !Session.PublishableKey.Equals(publishableKey.Trim(), StringComparison.Ordinal);

        Session.ProjectUrl = normalizedUrl;
        Session.PublishableKey = publishableKey.Trim();
        Preferences.Set(ProjectUrlKey, Session.ProjectUrl);
        Preferences.Set(PublishableKeyKey, Session.PublishableKey);

        if (configurationChanged && Session.IsSignedIn)
        {
            ClearSessionAsync().GetAwaiter().GetResult();
        }
    }

    public async Task SignUpAsync(string email, string password)
    {
        ValidateCredentials(email, password);
        EnsureConfigured();

        using var request = CreateRequest(HttpMethod.Post, "/auth/v1/signup", includeAuth: false);
        request.Content = CreateJsonContent(new { email = email.Trim(), password });
        using var response = await _httpClient.SendAsync(request);
        var auth = await ReadResponseAsync<AuthResponse>(response);

        if (!string.IsNullOrWhiteSpace(auth.AccessToken))
        {
            await SaveSessionAsync(auth, email.Trim());
        }
        else
        {
            Session.Email = email.Trim();
            Preferences.Set(EmailKey, Session.Email);
        }
    }

    public async Task SignInAsync(string email, string password)
    {
        ValidateCredentials(email, password);
        EnsureConfigured();

        using var request = CreateRequest(
            HttpMethod.Post,
            "/auth/v1/token?grant_type=password",
            includeAuth: false);
        request.Content = CreateJsonContent(new { email = email.Trim(), password });
        using var response = await _httpClient.SendAsync(request);
        var auth = await ReadResponseAsync<AuthResponse>(response);
        await SaveSessionAsync(auth, email.Trim());
    }

    public async Task SignOutAsync()
    {
        try
        {
            if (Session.IsConfigured && Session.IsSignedIn)
            {
                using var request = CreateRequest(HttpMethod.Post, "/auth/v1/logout", includeAuth: true);
                using var response = await _httpClient.SendAsync(request);
            }
        }
        finally
        {
            await ClearSessionAsync();
        }
    }

    public async Task UpsertBackupAsync(DataBackup backup)
    {
        await EnsureActiveSessionAsync();

        using var request = CreateRequest(
            HttpMethod.Post,
            "/rest/v1/user_backups?on_conflict=user_id",
            includeAuth: true);
        request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");
        request.Content = CreateJsonContent(new[]
        {
            new BackupRow
            {
                UserId = Session.UserId,
                Payload = backup,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        });

        using var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    public async Task<DataBackup?> GetBackupAsync()
    {
        await EnsureActiveSessionAsync();

        var userId = Uri.EscapeDataString(Session.UserId);
        using var request = CreateRequest(
            HttpMethod.Get,
            $"/rest/v1/user_backups?select=payload&user_id=eq.{userId}&limit=1",
            includeAuth: true);
        using var response = await _httpClient.SendAsync(request);
        var rows = await ReadResponseAsync<List<BackupRow>>(response);
        return rows.FirstOrDefault()?.Payload;
    }

    public async Task<string> UploadStatementAsync(Stream content, string objectPath, string contentType)
    {
        await EnsureActiveSessionAsync();

        var normalizedPath = string.Join('/', objectPath
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));
        var storagePath = $"{Session.UserId}/{normalizedPath}";
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/storage/v1/object/statements/{storagePath}",
            includeAuth: true);
        request.Headers.TryAddWithoutValidation("x-upsert", "true");
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        return storagePath;
    }

    private async Task EnsureActiveSessionAsync()
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(Session.RefreshToken))
        {
            throw new InvalidOperationException("Sign in before using cloud sync.");
        }

        if (!Session.IsSignedIn || Session.ExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            await RefreshSessionAsync();
        }
    }

    private async Task RefreshSessionAsync()
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            "/auth/v1/token?grant_type=refresh_token",
            includeAuth: false);
        request.Content = CreateJsonContent(new { refresh_token = Session.RefreshToken });
        using var response = await _httpClient.SendAsync(request);
        var auth = await ReadResponseAsync<AuthResponse>(response);
        await SaveSessionAsync(auth, Session.Email);
    }

    private async Task SaveSessionAsync(AuthResponse auth, string fallbackEmail)
    {
        if (string.IsNullOrWhiteSpace(auth.AccessToken)
            || string.IsNullOrWhiteSpace(auth.RefreshToken)
            || string.IsNullOrWhiteSpace(auth.User?.Id))
        {
            throw new InvalidOperationException("Supabase returned an incomplete session.");
        }

        Session.AccessToken = auth.AccessToken;
        Session.RefreshToken = auth.RefreshToken;
        Session.UserId = auth.User.Id;
        Session.Email = auth.User.Email ?? fallbackEmail;
        Session.ExpiresAtUtc = auth.ExpiresAt > 0
            ? DateTimeOffset.FromUnixTimeSeconds(auth.ExpiresAt)
            : DateTimeOffset.UtcNow.AddSeconds(auth.ExpiresIn);

        Preferences.Set(EmailKey, Session.Email);
        await SecureStorage.Default.SetAsync(AccessTokenKey, Session.AccessToken);
        await SecureStorage.Default.SetAsync(RefreshTokenKey, Session.RefreshToken);
        await SecureStorage.Default.SetAsync(UserIdKey, Session.UserId);
        await SecureStorage.Default.SetAsync(
            ExpiresAtKey,
            Session.ExpiresAtUtc.ToUnixTimeSeconds().ToString());
    }

    private async Task ClearSessionAsync()
    {
        Session.AccessToken = string.Empty;
        Session.RefreshToken = string.Empty;
        Session.UserId = string.Empty;
        Session.ExpiresAtUtc = default;
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        SecureStorage.Default.Remove(UserIdKey);
        SecureStorage.Default.Remove(ExpiresAtKey);
        await Task.CompletedTask;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, bool includeAuth)
    {
        EnsureConfigured();
        var request = new HttpRequestMessage(method, $"{Session.ProjectUrl}{path}");
        request.Headers.TryAddWithoutValidation("apikey", Session.PublishableKey);
        if (includeAuth)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Session.AccessToken);
        }

        return request;
    }

    private static StringContent CreateJsonContent(object value) =>
        new(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(GetErrorMessage(json, response));
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException("Supabase returned an invalid response.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var json = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(GetErrorMessage(json, response));
    }

    private static string GetErrorMessage(string json, HttpResponseMessage response)
    {
        try
        {
            var error = JsonSerializer.Deserialize<SupabaseError>(json, JsonOptions);
            return error?.Message
                ?? error?.ErrorDescription
                ?? error?.Error
                ?? $"Supabase request failed ({(int)response.StatusCode}).";
        }
        catch (JsonException)
        {
            return $"Supabase request failed ({(int)response.StatusCode}).";
        }
    }

    private void EnsureConfigured()
    {
        if (!Session.IsConfigured)
        {
            throw new InvalidOperationException("Configure the Supabase project URL and publishable key first.");
        }
    }

    private static void ValidateCredentials(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new InvalidOperationException("Enter a valid email.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw new InvalidOperationException("Password must be at least 6 characters.");
        }
    }

    private static async Task<string> GetSecureValueAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key) ?? string.Empty;
        }
        catch
        {
            SecureStorage.Default.Remove(key);
            return string.Empty;
        }
    }

    private sealed class AuthResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public long ExpiresIn { get; set; }

        [JsonPropertyName("expires_at")]
        public long ExpiresAt { get; set; }

        [JsonPropertyName("user")]
        public AuthUser? User { get; set; }
    }

    private sealed class AuthUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }

    private sealed class BackupRow
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("payload")]
        public DataBackup Payload { get; set; } = new();

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private sealed class SupabaseError
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("msg")]
        public string? Msg
        {
            set => Message ??= value;
        }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
