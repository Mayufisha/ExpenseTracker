using SQLite;

namespace ExpenseTracker.Models;

public class ExpenseSplit
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string SyncId { get; set; } = Guid.NewGuid().ToString("N");
    public string TransactionSyncId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal UserShare { get; set; }
    public string Currency { get; set; } = "CAD";
    public string SplitMethod { get; set; } = "Equal";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public List<SplitParticipant> Participants { get; set; } = new();

    [Ignore]
    public decimal AmountOwed => Participants.Sum(participant => participant.AmountOwed);

    [Ignore]
    public decimal AmountCollected => Participants
        .Where(participant => participant.IsPaid)
        .Sum(participant => participant.AmountOwed);

    [Ignore]
    public decimal AmountOutstanding => AmountOwed - AmountCollected;

    [Ignore]
    public bool IsSettled => Participants.Count > 0 && Participants.All(participant => participant.IsPaid);

    [Ignore]
    public double CollectionProgress => AmountOwed <= 0
        ? 0
        : Math.Clamp((double)(AmountCollected / AmountOwed), 0, 1);

    [Ignore]
    public string ParticipantSummary => Participants.Count == 1
        ? Participants[0].Name
        : $"{Participants.Count} people";

    [Ignore]
    public string StatusDisplay => IsSettled ? "Settled" : $"{Currency} {AmountOutstanding:F2} outstanding";
}
