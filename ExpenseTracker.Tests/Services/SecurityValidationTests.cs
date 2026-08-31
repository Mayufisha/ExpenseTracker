using ExpenseTracker.Models;
using ExpenseTracker.Security;
using ExpenseTracker.Services;

namespace ExpenseTracker.Tests.Services;

public class SecurityValidationTests
{
    [Fact]
    public void CredentialPolicy_RejectsWeakOrExcessiveCredentials()
    {
        Assert.False(CredentialPolicy.IsValidEmail("not-an-email"));
        Assert.Null(CredentialPolicy.GetPasswordError("a unique passphrase with length"));
        Assert.NotNull(CredentialPolicy.GetPasswordError("short"));
        Assert.Null(CredentialPolicy.GetPasswordError("short", enforceMinimumLength: false));
        Assert.NotNull(CredentialPolicy.GetPasswordError(new string('a', 129)));
    }

    [Fact]
    public void UserDataContext_RejectsPathTraversalIdentifiers()
    {
        var context = new UserDataContext();

        Assert.Throws<InvalidOperationException>(() => context.SetCurrentUser("../other-user"));
        Assert.Throws<InvalidOperationException>(() => context.SetCurrentUser("user/other"));
        context.SetCurrentUser("550e8400-e29b-41d4-a716-446655440000");

        Assert.Equal("550e8400-e29b-41d4-a716-446655440000", context.CurrentUserId);
    }

    [Fact]
    public async Task StatementFileValidator_RejectsOversizedAndSpoofedFiles()
    {
        var oversizedPath = Path.Combine(Path.GetTempPath(), $"oversized-{Guid.NewGuid():N}.csv");
        var spoofedPath = Path.Combine(Path.GetTempPath(), $"spoofed-{Guid.NewGuid():N}.pdf");

        try
        {
            await using var oversized = new MemoryStream(new byte[StatementFileValidator.MaximumFileSize + 1]);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                StatementFileValidator.CopyAndValidateAsync(oversized, oversizedPath, ".csv"));

            await using var spoofed = new MemoryStream("Description,Amount\nTest,10.00"u8.ToArray());
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                StatementFileValidator.CopyAndValidateAsync(spoofed, spoofedPath, ".pdf"));
        }
        finally
        {
            if (File.Exists(oversizedPath)) File.Delete(oversizedPath);
            if (File.Exists(spoofedPath)) File.Delete(spoofedPath);
        }
    }

    [Fact]
    public void DataBackupValidator_RejectsUnsafeCloudPaths()
    {
        var backup = new DataBackup
        {
            Statements =
            [
                new StatementBackupItem
                {
                    InstitutionName = "Bank",
                    AccountName = "Chequing",
                    OriginalFileName = "statement.csv",
                    CloudStoragePath = "../another-user/statement.csv",
                    FileType = "CSV",
                    FileHash = new string('A', 64)
                }
            ]
        };

        Assert.Throws<InvalidDataException>(() => DataBackupValidator.Validate(backup));
    }
}
