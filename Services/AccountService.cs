using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public class AccountService : IAccountService
{
    private const string ServerUrlKey = "Account.ServerUrl";
    private const string EmailKey = "Account.Email";
    private const string TokenKey = "Account.Token";

    private readonly IBackupService _backupService;
    private readonly HttpClient _httpClient = new();

    public AccountSession Session { get; } = new();

    public AccountService(IBackupService backupService)
    {
        _backupService = backupService;
        Session.ServerUrl = Preferences.Get(ServerUrlKey, string.Empty);
        Session.Email = Preferences.Get(EmailKey, string.Empty);
        Session.AccessToken = Preferences.Get(TokenKey, string.Empty);
    }

    public void SetServerUrl(string serverUrl)
    {
        Session.ServerUrl = NormalizeUrl(serverUrl);
        Preferences.Set(ServerUrlKey, Session.ServerUrl);
    }

    public async Task RegisterAsync(string email, string password)
    {
        ValidateCredentials(email, password);
        EnsureServerUrl();

        var payload = new { email = email.Trim(), password };
        using var response = await PostJsonAsync("/api/account/register", payload, includeAuth: false);
        response.EnsureSuccessStatusCode();
    }

    public async Task SignInAsync(string email, string password)
    {
        ValidateCredentials(email, password);
        EnsureServerUrl();

        var payload = new { email = email.Trim(), password };
        using var response = await PostJsonAsync("/api/account/login", payload, includeAuth: false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var login = JsonSerializer.Deserialize<LoginResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException("Invalid login response.");

        if (string.IsNullOrWhiteSpace(login.Token))
        {
            throw new InvalidOperationException("Missing access token from server response.");
        }

        Session.Email = email.Trim();
        Session.AccessToken = login.Token;
        Preferences.Set(EmailKey, Session.Email);
        Preferences.Set(TokenKey, Session.AccessToken);
    }

    public void SignOut()
    {
        Session.Email = string.Empty;
        Session.AccessToken = string.Empty;
        Preferences.Remove(EmailKey);
        Preferences.Remove(TokenKey);
    }

    public async Task PushToCloudAsync()
    {
        EnsureSignedIn();
        var backup = await _backupService.CreateBackupAsync();
        using var response = await PostJsonAsync("/api/sync/push", backup, includeAuth: true);
        response.EnsureSuccessStatusCode();
    }

    public async Task<BackupImportResult> PullFromCloudAsync()
    {
        EnsureSignedIn();

        var request = CreateRequest(HttpMethod.Get, "/api/sync/pull", includeAuth: true);
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var backup = JsonSerializer.Deserialize<DataBackup>(json, JsonOptions)
            ?? throw new InvalidOperationException("Invalid cloud backup payload.");

        return await _backupService.ImportBackupAsync(backup, clearExistingData: true);
    }

    private async Task<HttpResponseMessage> PostJsonAsync(string path, object payload, bool includeAuth)
    {
        var request = CreateRequest(HttpMethod.Post, path, includeAuth);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _httpClient.SendAsync(request);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, bool includeAuth)
    {
        EnsureServerUrl();
        var url = $"{Session.ServerUrl.TrimEnd('/')}{path}";
        var request = new HttpRequestMessage(method, url);

        if (includeAuth)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Session.AccessToken);
        }

        return request;
    }

    private void EnsureServerUrl()
    {
        if (string.IsNullOrWhiteSpace(Session.ServerUrl))
        {
            throw new InvalidOperationException("Set a server URL before using account sync.");
        }
    }

    private void EnsureSignedIn()
    {
        EnsureServerUrl();

        if (!Session.IsSignedIn)
        {
            throw new InvalidOperationException("Sign in before syncing.");
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

    private static string NormalizeUrl(string serverUrl)
    {
        return serverUrl?.Trim() ?? string.Empty;
    }

    private sealed class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
