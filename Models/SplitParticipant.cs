using SQLite;

namespace ExpenseTracker.Models;

public class SplitParticipant
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string SyncId { get; set; } = Guid.NewGuid().ToString("N");
    public string ExpenseSplitSyncId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public decimal AmountOwed { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? LastPaymentRequestAt { get; set; }
    public string PaymentProvider { get; set; } = string.Empty;
    public string ExternalPaymentId { get; set; } = string.Empty;

    [Ignore]
    public string PaymentStatus => IsPaid ? "Paid" : "Owes";

    [Ignore]
    public string SettlementAction => IsPaid ? "Undo" : "Mark paid";

    [Ignore]
    public string AmountDisplay => $"${AmountOwed:F2}";
}
