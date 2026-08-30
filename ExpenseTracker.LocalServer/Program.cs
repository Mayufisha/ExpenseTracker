using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5088");
builder.Services.AddSingleton<LocalDataStore>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ready" }));

app.MapPost("/api/auth/signup", async (Credentials credentials, LocalDataStore store) =>
    await ExecuteAsync(() => store.SignUpAsync(credentials.Email, credentials.Password)));

app.MapPost("/api/auth/login", async (Credentials credentials, LocalDataStore store) =>
    await ExecuteAsync(() => store.SignInAsync(credentials.Email, credentials.Password)));

app.MapGet("/api/auth/session", async (HttpRequest request, LocalDataStore store) =>
    await ExecuteAsync(() => store.GetSessionAsync(GetBearerToken(request))));

app.MapPost("/api/auth/logout", async (HttpRequest request, LocalDataStore store) =>
    await ExecuteAsync(async () =>
    {
        await store.SignOutAsync(GetBearerToken(request));
        return new { signedOut = true };
    }));

app.MapPut("/api/backup", async (HttpRequest request, JsonElement backup, LocalDataStore store) =>
    await ExecuteAsync(async () =>
    {
        await store.SaveBackupAsync(GetBearerToken(request), backup);
        return new { saved = true };
    }));

app.MapGet("/api/backup", async (HttpRequest request, LocalDataStore store) =>
    await ExecuteAsync(() => store.GetBackupAsync(GetBearerToken(request))));

app.Run();

static string GetBearerToken(HttpRequest request)
{
    const string prefix = "Bearer ";
    var authorization = request.Headers.Authorization.ToString();
    if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        throw new LocalApiException(StatusCodes.Status401Unauthorized, "Sign in is required.");

    return authorization[prefix.Length..].Trim();
}

static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> action)
{
    try
    {
        return Results.Ok(await action());
    }
    catch (LocalApiException exception)
    {
        return Results.Json(new { message = exception.Message }, statusCode: exception.StatusCode);
    }
}

internal sealed record Credentials(string Email, string Password);

internal sealed class LocalDataStore
{
    private const int PasswordIterations = 210_000;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private LocalDatabase _database;

    public LocalDataStore()
    {
        var directory = Environment.GetEnvironmentVariable("MONEY_MANAGER_DATA_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MoneyManager",
                "LocalServer");
        }

        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "accounts.json");
        _database = LoadDatabase();
    }

    public async Task<AuthResult> SignUpAsync(string email, string password)
    {
        ValidateCredentials(email, password);
        await _gate.WaitAsync();
        try
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            if (_database.Users.Any(user => user.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)))
                throw new LocalApiException(StatusCodes.Status409Conflict, "An account with this email already exists.");

            var salt = RandomNumberGenerator.GetBytes(16);
            var user = new LocalUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = normalizedEmail,
                PasswordSalt = Convert.ToBase64String(salt),
                PasswordHash = Convert.ToBase64String(HashPassword(password, salt))
            };
            _database.Users.Add(user);
            var result = CreateSession(user);
            await SaveDatabaseAsync();
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AuthResult> SignInAsync(string email, string password)
    {
        ValidateCredentials(email, password);
        await _gate.WaitAsync();
        try
        {
            var user = _database.Users.FirstOrDefault(candidate =>
                candidate.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase));
            if (user is null || !VerifyPassword(user, password))
                throw new LocalApiException(StatusCodes.Status401Unauthorized, "Email or password is incorrect.");

            RemoveExpiredSessions(user);
            var result = CreateSession(user);
            await SaveDatabaseAsync();
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AuthResult> GetSessionAsync(string token)
    {
        await _gate.WaitAsync();
        try
        {
            var user = FindUserByToken(token);
            var session = user.Sessions.First(record =>
                CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(record.TokenHash),
                    SHA256.HashData(Encoding.UTF8.GetBytes(token))));
            return new AuthResult(string.Empty, user.Id, user.Email, session.ExpiresAtUtc);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SignOutAsync(string token)
    {
        await _gate.WaitAsync();
        try
        {
            var tokenHash = HashToken(token);
            foreach (var user in _database.Users)
                user.Sessions.RemoveAll(session => session.TokenHash == tokenHash);
            await SaveDatabaseAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveBackupAsync(string token, JsonElement backup)
    {
        await _gate.WaitAsync();
        try
        {
            var user = FindUserByToken(token);
            user.Backup = backup.Clone();
            await SaveDatabaseAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<JsonElement> GetBackupAsync(string token)
    {
        await _gate.WaitAsync();
        try
        {
            var user = FindUserByToken(token);
            return user.Backup?.Clone()
                ?? throw new LocalApiException(StatusCodes.Status404NotFound, "No local-server backup exists for this account yet.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private LocalUser FindUserByToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new LocalApiException(StatusCodes.Status401Unauthorized, "Sign in is required.");

        var tokenHash = HashToken(token);
        var user = _database.Users.FirstOrDefault(candidate => candidate.Sessions.Any(session =>
            session.TokenHash == tokenHash && session.ExpiresAtUtc > DateTimeOffset.UtcNow));
        return user ?? throw new LocalApiException(StatusCodes.Status401Unauthorized, "The saved session has expired. Sign in again.");
    }

    private static bool VerifyPassword(LocalUser user, string password)
    {
        var salt = Convert.FromBase64String(user.PasswordSalt);
        var expected = Convert.FromBase64String(user.PasswordHash);
        var actual = HashPassword(password, salt);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static byte[] HashPassword(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, 32);

    private AuthResult CreateSession(LocalUser user)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var expiresAt = DateTimeOffset.UtcNow.Add(SessionLifetime);
        user.Sessions.Add(new LocalSession { TokenHash = HashToken(token), ExpiresAtUtc = expiresAt });
        return new AuthResult(token, user.Id, user.Email, expiresAt);
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static void RemoveExpiredSessions(LocalUser user) =>
        user.Sessions.RemoveAll(session => session.ExpiresAtUtc <= DateTimeOffset.UtcNow);

    private static void ValidateCredentials(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new LocalApiException(StatusCodes.Status400BadRequest, "Enter a valid email.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            throw new LocalApiException(StatusCodes.Status400BadRequest, "Password must be at least 6 characters.");
    }

    private LocalDatabase LoadDatabase()
    {
        if (!File.Exists(_filePath)) return new LocalDatabase();
        try
        {
            return JsonSerializer.Deserialize<LocalDatabase>(File.ReadAllText(_filePath), _jsonOptions)
                ?? new LocalDatabase();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"The local account store is invalid: {_filePath}");
        }
    }

    private async Task SaveDatabaseAsync()
    {
        var temporaryPath = $"{_filePath}.tmp";
        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(_database, _jsonOptions));
        File.Move(temporaryPath, _filePath, true);
    }
}

internal sealed class LocalDatabase
{
    public List<LocalUser> Users { get; set; } = [];
}

internal sealed class LocalUser
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public List<LocalSession> Sessions { get; set; } = [];
    public JsonElement? Backup { get; set; }
}

internal sealed class LocalSession
{
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

internal sealed record AuthResult(
    string AccessToken,
    string UserId,
    string Email,
    DateTimeOffset ExpiresAtUtc);

internal sealed class LocalApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
