using SQLite;

namespace ExpenseTracker.Services;

internal static class SQLiteSchema
{
    public static async Task EnsureOwnerColumnAsync(SQLiteAsyncConnection database, string tableName)
    {
        var quotedTable = tableName.Replace("\"", "\"\"");
        var columns = await database.GetTableInfoAsync(tableName);
        if (columns.All(column => !column.Name.Equals("OwnerUserId", StringComparison.OrdinalIgnoreCase)))
        {
            await database.ExecuteAsync(
                $"ALTER TABLE \"{quotedTable}\" ADD COLUMN OwnerUserId TEXT NOT NULL DEFAULT ''");
        }

        await database.ExecuteAsync(
            $"CREATE INDEX IF NOT EXISTS \"IX_{quotedTable}_OwnerUserId\" ON \"{quotedTable}\" (OwnerUserId)");
    }

    public static async Task EnsureTextColumnAsync(
        SQLiteAsyncConnection database,
        string tableName,
        string columnName)
    {
        var columns = await database.GetTableInfoAsync(tableName);
        if (columns.All(column => !column.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase)))
        {
            await database.ExecuteAsync(
                $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" TEXT NOT NULL DEFAULT ''");
        }
    }
}
