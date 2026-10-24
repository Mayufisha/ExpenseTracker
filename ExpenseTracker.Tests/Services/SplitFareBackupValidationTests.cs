using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Tests.Services;

public class SplitFareBackupValidationTests
{
    private static DataBackup Backup() => new()
    {
        ExpenseSplits = [new ExpenseSplitBackupItem
        {
            SyncId = "split-1", TransactionSyncId = "transaction-1", Title = "Fare", TotalAmount = 30m,
            Participants = [new SplitParticipantBackupItem { SyncId = "person-1", Name = "Alex", AmountOwed = 10m }]
        }]
    };

    [Fact]
    public void ValidOwnerManagedSplit_PassesPreValidation() => DataBackupValidator.Validate(Backup());

    [Theory]
    [InlineData("0")]
    [InlineData("10.001")]
    [InlineData("31")]
    public void InvalidShare_IsRejectedBeforeRestoreCanClearExistingData(string value)
    {
        var backup = Backup();
        backup.ExpenseSplits[0].Participants[0].AmountOwed = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<InvalidDataException>(() => DataBackupValidator.Validate(backup));
    }
    [Fact]
    public void DuplicateSplitTransaction_IsRejectedBeforeImport()
    {
        var backup = Backup();
        var second = Backup().ExpenseSplits[0];
        second.SyncId = "split-2";
        second.Participants[0].SyncId = "person-2";
        backup.ExpenseSplits.Add(second);
        Assert.Throws<InvalidDataException>(() => DataBackupValidator.Validate(backup));
    }
    [Fact]
    public void DuplicateParticipantName_IsRejectedBeforeImport()
    {
        var backup = Backup();
        backup.ExpenseSplits[0].Participants.Add(new SplitParticipantBackupItem
            { SyncId = "person-2", Name = " alex ", AmountOwed = 10m });
        Assert.Throws<InvalidDataException>(() => DataBackupValidator.Validate(backup));
    }
}
