using SQLite;

namespace ExpenseTracker.Models;

public class ScheduledTransaction
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public decimal Amount { get; set; }
    public bool IsIncome { get; set; }
    public int CategoryId { get; set; }
    public DateTime ScheduledDate { get; set; }

    public string Note { get; set; } = string.Empty;

    public string Frequency { get; set; } = "None";
}
