using SQLite;

namespace ExpenseTracker.Models;

public class Transaction
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public decimal Amount { get; set; }
    public bool IsIncome { get; set; }
    public int CategoryId { get; set; }
    public DateTime Date { get; set; }
    public string Note { get; set; } = string.Empty;

    [Ignore]
    public Category Category { get; set; }
}
