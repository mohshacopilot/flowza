using Microsoft.Data.SqlClient;

namespace Flowza.Testing;

/// <summary>Helpers that put a <c>flowza_test_*</c> database back into a known state.</summary>
public static class TestDatabase
{
    private const string DropServiceTablesSql = """
        DECLARE @sql nvarchar(max) = N'';
        SELECT @sql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
        FROM sys.foreign_keys fk
        JOIN sys.tables t ON fk.parent_object_id = t.object_id
        JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE s.name IN (N'dbo', N'wolverine');
        EXEC (@sql);

        SET @sql = N'';
        SELECT @sql += N'DROP TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';'
        FROM sys.tables t
        JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE s.name IN (N'dbo', N'wolverine');
        EXEC (@sql);
        """;

    /// <summary>
    /// Drops every table a service owns in its test database (schemas <c>dbo</c>, including the EF migrations
    /// history, and <c>wolverine</c>), so the next start behaves like a first start. Tables of other test schemas
    /// are left alone.
    /// </summary>
    public static async Task DropServiceTablesAsync(string database, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(TestSqlServer.ConnectionString(database));
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(DropServiceTablesSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
