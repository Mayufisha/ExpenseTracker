using SQLite;

namespace ExpenseTracker.Models;

public class Goal
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;       
    public decimal TargetAmount { get; set; }               
    public decimal CurrentAmount { get; set; }              
    public DateTime? Deadline { get; set; }                
    public bool IsCompleted { get; set; }

    [Ignore]
    public double Progress =>
       TargetAmount <= 0 ? 0 : Math.Clamp((double)(CurrentAmount / TargetAmount), 0, 1);
}
