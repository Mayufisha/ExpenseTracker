using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using ExpenseTracker.Security;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5088");
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 5 * 1024 * 1024;
    options.Limits.MaxRequestHeaderCount = 50;
    options.Limits.MaxRequestHeadersTotalSize = 16 * 1024;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
    options.Limits.MaxConcurrentConnections = 100;
});
builder.Services.AddSingleton<LocalDataStore>();
builder.Services.AddSingleton<SecurityEventRecorder>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Too many requests. Wait before trying again.\"}",
            cancellationToken);
    };
    options.AddSlidingWindowLimiter("authentication", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.SegmentsPerWindow = 6;
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.AutoReplenishment = true;
    });
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit = 120;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.AutoReplenishment = true;
    });
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store, max-age=0";
    context.Response.Headers.Pragma = "no-cache";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    var rawTarget = context.Features.Get<IHttpRequestFeature>()?.RawTarget
        ?? context.Request.Path.Value
        ?? string.Empty;
    var queryStart = rawTarget.IndexOf('?');
    var rawPath = queryStart >= 0 ? rawTarget[..queryStart] : rawTarget;
    var decision = RequestFirewallPolicy.Inspect(new FirewallRequest(
        context.Request.Method.ToUpperInvariant(),
        rawPath,
        context.Request.Host.Host,
        context.Connection.RemoteIpAddress is { } remoteAddress && IPAddress.IsLoopback(remoteAddress),
        context.Request.ContentLength,
        context.Request.ContentType,
        context.Request.Headers.ContainsKey("Forwarded")
            || context.Request.Headers.ContainsKey("X-Forwarded-For")
            || context.Request.Headers.ContainsKey("X-Forwarded-Host")
            || context.Request.Headers.ContainsKey("X-Forwarded-Proto"),
        context.Request.QueryString.Value?.Length ?? 0));
    if (!decision.IsAllowed)
    {
        var recorder = context.RequestServices.GetRequiredService<SecurityEventRecorder>();
        await recorder.RecordAsync(context, decision.EventType);
        if (decision.IsDecoy) await Task.Delay(Random.Shared.Next(40, 121));
        context.Response.StatusCode = decision.StatusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"message\":\"The request was rejected.\"}");
        return;
    }

    if (rawPath is "/api/auth/signup" or "/api/auth/login")
    {
        var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySizeFeature is { IsReadOnly: false }) bodySizeFeature.MaxRequestBodySize = 4 * 1024;
    }

    await next();

    if (context.Response.StatusCode is StatusCodes.Status401Unauthorized
        or StatusCodes.Status429TooManyRequests)
    {
        var recorder = context.RequestServices.GetRequiredService<SecurityEventRecorder>();
        var eventType = context.Response.StatusCode == StatusCodes.Status401Unauthorized
            ? "authentication_rejected"
            : "rate_limit_triggered";
        await recorder.RecordAsync(context, eventType);
    }
});
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ready" }));

app.MapPost("/api/auth/signup", async (Credentials credentials, LocalDataStore store) =>
    await ExecuteAsync(() => store.SignUpAsync(credentials.Email, credentials.Password), app.Logger))
    .RequireRateLimiting("authentication");

app.MapPost("/api/auth/login", async (Credentials credentials, LocalDataStore store) =>
    await ExecuteAsync(() => store.SignInAsync(credentials.Email, credentials.Password), app.Logger))
    .RequireRateLimiting("authentication");

app.MapGet("/api/auth/session", async (HttpRequest request, LocalDataStore store) =>
    await ExecuteAsync(() => store.GetSessionAsync(GetBearerToken(request)), app.Logger))
    .RequireRateLimiting("api");

app.MapPost("/api/auth/logout", async (HttpRequest request, LocalDataStore store) =>
    await ExecuteAsync(async () =>
    {
        await store.SignOutAsync(GetBearerToken(request));
        return new { signedOut = true };
    }, app.Logger))
    .RequireRateLimiting("api");

app.MapPut("/api/backup", async (HttpRequest request, JsonElement backup, LocalDataStore store) =>
    await ExecuteAsync(async () =>
    {
        await store.SaveBackupAsync(GetBearerToken(request), backup);
        return new { saved = true };
    }, app.Logger))
    .RequireRateLimiting("api");

app.MapGet("/api/backup", async (HttpRequest request, LocalDataStore store) =>
    await ExecuteAsync(() => store.GetBackupAsync(GetBearerToken(request)), app.Logger))
    .RequireRateLimiting("api");

app.MapFallback(async (HttpContext context, SecurityEventRecorder recorder) =>
{
    await recorder.RecordAsync(context, "unknown_route");
    return Results.NotFound(new { message = "The requested resource was not found." });
});

app.Run();

static string GetBearerToken(HttpRequest request)
{
    const string prefix = "Bearer ";
    var authorization = request.Headers.Authorization.ToString();
    if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        throw new LocalApiException(StatusCodes.Status401Unauthorized, "Sign in is required.");

    var token = authorization[prefix.Length..].Trim();
    if (token.Length != 43 || token.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
        throw new LocalApiException(StatusCodes.Status401Unauthorized, "The session is invalid or expired.");
    return token;
}

static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> action, ILogger logger)
{
    try
    {
        return Results.Ok(await action());
    }
    catch (LocalApiException exception)
    {
        return Results.Json(new { message = exception.Message }, statusCode: exception.StatusCode);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Unhandled local API error");
        return Results.Json(
            new { message = "The local server could not complete the request." },
            statusCode: StatusCodes.Status500InternalServerError);
    }
}

internal sealed record Credentials(string Email, string Password);

internal sealed class LocalDataStore
{
    private const int CurrentPasswordIterations = 600_000;
    private const int LegacyPasswordIterations = 210_000;
    private const int MaximumSessionsPerUser = 5;
    private const int MaximumBackupBytes = 4 * 1024 * 1024;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true, MaxDepth = 32 };
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
        ValidateCredentials(email, password, enforceMinimumPasswordLength: true);
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
                PasswordHash = Convert.ToBase64String(HashPassword(password, salt, CurrentPasswordIterations)),
                PasswordIterations = CurrentPasswordIterations
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
        ValidateCredentials(email, password, enforceMinimumPasswordLength: false);
        await _gate.WaitAsync();
        try
        {
            var user = _database.Users.FirstOrDefault(candidate =>
                candidate.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase));
            if (user is null || !VerifyPassword(user, password))
                throw new LocalApiException(StatusCodes.Status401Unauthorized, "Email or password is incorrect.");

            UpgradePasswordHashIfNeeded(user, password);
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
            var tokenHash = HashToken(token);
            var session = user.Sessions.First(record => TokenHashesEqual(record.TokenHash, tokenHash));
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
                user.Sessions.RemoveAll(session => TokenHashesEqual(session.TokenHash, tokenHash));
            await SaveDatabaseAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveBackupAsync(string token, JsonElement backup)
    {
        if (backup.ValueKind != JsonValueKind.Object
            || Encoding.UTF8.GetByteCount(backup.GetRawText()) > MaximumBackupBytes)
            throw new LocalApiException(StatusCodes.Status400BadRequest, "The backup payload is invalid or too large.");

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
            TokenHashesEqual(session.TokenHash, tokenHash) && session.ExpiresAtUtc > DateTimeOffset.UtcNow));
        return user ?? throw new LocalApiException(StatusCodes.Status401Unauthorized, "The saved session has expired. Sign in again.");
    }

    private static bool VerifyPassword(LocalUser user, string password)
    {
        try
        {
            var iterations = user.PasswordIterations <= 0 ? LegacyPasswordIterations : user.PasswordIterations;
            if (iterations is < LegacyPasswordIterations or > 2_000_000) return false;
            var salt = Convert.FromBase64String(user.PasswordSalt);
            var expected = Convert.FromBase64String(user.PasswordHash);
            var actual = HashPassword(password, salt, iterations);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] HashPassword(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);

    private static void UpgradePasswordHashIfNeeded(LocalUser user, string password)
    {
        if (user.PasswordIterations >= CurrentPasswordIterations) return;

        var salt = RandomNumberGenerator.GetBytes(16);
        user.PasswordSalt = Convert.ToBase64String(salt);
        user.PasswordHash = Convert.ToBase64String(HashPassword(password, salt, CurrentPasswordIterations));
        user.PasswordIterations = CurrentPasswordIterations;
    }

    private AuthResult CreateSession(LocalUser user)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var expiresAt = DateTimeOffset.UtcNow.Add(SessionLifetime);
        RemoveExpiredSessions(user);
        while (user.Sessions.Count >= MaximumSessionsPerUser)
        {
            var oldest = user.Sessions.MinBy(session => session.ExpiresAtUtc);
            if (oldest == null) break;
            user.Sessions.Remove(oldest);
        }
        user.Sessions.Add(new LocalSession { TokenHash = HashToken(token), ExpiresAtUtc = expiresAt });
        return new AuthResult(token, user.Id, user.Email, expiresAt);
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool TokenHashesEqual(string storedHash, string candidateHash)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(storedHash),
                Convert.FromHexString(candidateHash));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void RemoveExpiredSessions(LocalUser user) =>
        user.Sessions.RemoveAll(session => session.ExpiresAtUtc <= DateTimeOffset.UtcNow);

    private static void ValidateCredentials(
        string email,
        string password,
        bool enforceMinimumPasswordLength)
    {
        if (!CredentialPolicy.IsValidEmail(email))
            throw new LocalApiException(StatusCodes.Status400BadRequest, "Enter a valid email.");
        var passwordError = CredentialPolicy.GetPasswordError(password, enforceMinimumPasswordLength);
        if (passwordError != null)
            throw new LocalApiException(StatusCodes.Status400BadRequest, passwordError);
    }

    private LocalDatabase LoadDatabase()
    {
        if (!File.Exists(_filePath)) return new LocalDatabase();
        if (new FileInfo(_filePath).Length > 100 * 1024 * 1024)
            throw new InvalidOperationException($"The local account store is too large: {_filePath}");
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
    public int PasswordIterations { get; set; } = 210_000;
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
