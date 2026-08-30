using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public sealed class LocalServerAccountService : IAccountService
{
    private const string EmailKey = "LocalServer.Email";
    private const string AccessTokenKey = "LocalServer.AccessToken";
    private const string UserIdKey = "LocalServer.UserId";
    private const string ExpiresAtKey = "LocalServer.ExpiresAtUtc";
    private readonly IBackupService _backupService;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public AccountSession Session { get; } = new();
    public string BackendName => "Local test server";

    public LocalServerAccountService(
        LocalServerOptions options,
        IBackupService backupService,
        HttpClient httpClient)
    {
        _backupService = backupService;
        _httpClient = httpClient;
        _baseUrl = options.BaseUrl.TrimEnd('/');
        Session.ProjectUrl = _baseUrl;
        Session.PublishableKey = "local-development";
        Session.Email = Preferences.Get(EmailKey, string.Empty);
    }

    public async Task InitializeAsync()
    {
        var savedToken = await GetSecureValueAsync(AccessTokenKey);
        if (string.IsNullOrWhiteSpace(savedToken)) return;

        try
        {
            using var request = CreateRequest(HttpMethod.Get, "/api/auth/session", savedToken);
            using var response = await _httpClient.SendAsync(request);
            var result = await ReadResponseAsync<LocalAuthResult>(response);
            await SaveSessionAsync(result, savedToken);
        }
        catch (HttpRequestException)
        {
            ClearRuntimeSession();
        }
        catch (TaskCanceledException)
        {
            ClearRuntimeSession();
        }
        catch (InvalidOperationException)
        {
            await ClearSessionAsync();
        }
    }

    public async Task RegisterAsync(string email, string password)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/auth/signup");
        request.Content = JsonContent.Create(new { email = email.Trim(), password });
        using var response = await SendAsync(request);
        await SaveSessionAsync(await ReadResponseAsync<LocalAuthResult>(response));
    }

    public async Task SignInAsync(string email, string password)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/auth/login");
        request.Content = JsonContent.Create(new { email = email.Trim(), password });
        using var response = await SendAsync(request);
        await SaveSessionAsync(await ReadResponseAsync<LocalAuthResult>(response));
    }

    public async Task SignOutAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(Session.AccessToken))
            {
                using var request = CreateRequest(HttpMethod.Post, "/api/auth/logout", Session.AccessToken);
                using var response = await SendAsync(request);
            }
        }
        catch (HttpRequestException)
        {
        }
        finally
        {
            await ClearSessionAsync();
        }
    }

    public async Task PushToCloudAsync()
    {
        EnsureSignedIn();
        var backup = await _backupService.CreateBackupAsync();
        using var request = CreateRequest(HttpMethod.Put, "/api/backup", Session.AccessToken);
        request.Content = JsonContent.Create(backup);
        using var response = await SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    public async Task<BackupImportResult> PullFromCloudAsync()
    {
        EnsureSignedIn();
        using var request = CreateRequest(HttpMethod.Get, "/api/backup", Session.AccessToken);
        using var response = await SendAsync(request);
        var backup = await ReadResponseAsync<DataBackup>(response);
        return await _backupService.ImportBackupAsync(backup, clearExistingData: true);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string? token = null)
    {
        var request = new HttpRequestMessage(method, $"{_baseUrl}{path}");
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        try
        {
            return await _httpClient.SendAsync(request);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                $"The local test server is not running. Start it at {_baseUrl} and try again.",
                exception);
        }
    }

    private async Task SaveSessionAsync(LocalAuthResult result, string? existingToken = null)
    {
        var token = string.IsNullOrWhiteSpace(result.AccessToken) ? existingToken : result.AccessToken;
        if (string.IsNullOrWhiteSpace(token)
            || string.IsNullOrWhiteSpace(result.UserId)
            || string.IsNullOrWhiteSpace(result.Email))
            throw new InvalidOperationException("The local test server returned an invalid session.");

        Session.AccessToken = token;
        Session.UserId = result.UserId;
        Session.Email = result.Email;
        Session.ExpiresAtUtc = result.ExpiresAtUtc;
        Preferences.Set(EmailKey, Session.Email);
        await SecureStorage.Default.SetAsync(AccessTokenKey, Session.AccessToken);
        await SecureStorage.Default.SetAsync(UserIdKey, Session.UserId);
        await SecureStorage.Default.SetAsync(ExpiresAtKey, Session.ExpiresAtUtc.ToUnixTimeSeconds().ToString());
    }

    private async Task ClearSessionAsync()
    {
        ClearRuntimeSession();
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(UserIdKey);
        SecureStorage.Default.Remove(ExpiresAtKey);
        await Task.CompletedTask;
    }

    private void ClearRuntimeSession()
    {
        Session.AccessToken = string.Empty;
        Session.UserId = string.Empty;
        Session.ExpiresAtUtc = default;
    }

    private void EnsureSignedIn()
    {
        if (!Session.IsSignedIn)
            throw new InvalidOperationException("Sign in before using local-server sync.");
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(GetErrorMessage(json, response));

        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException("The local test server returned an invalid response.");
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
            var error = JsonSerializer.Deserialize<LocalError>(json, JsonOptions);
            return error?.Message ?? $"Local server request failed ({(int)response.StatusCode}).";
        }
        catch (JsonException)
        {
            return $"Local server request failed ({(int)response.StatusCode}).";
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

    private sealed class LocalAuthResult
    {
        public string AccessToken { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAtUtc { get; set; }
    }

    private sealed class LocalError
    {
        public string? Message { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
