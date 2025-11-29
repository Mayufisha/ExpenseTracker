using SQLite;

namespace ExpenseTracker.Models;

public class Category
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#2196F3";
}
