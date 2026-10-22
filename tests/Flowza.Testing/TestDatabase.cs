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
    /// Empties schemas completely (tables, procedures, table types), as on a database the DBA has just created. The
    /// schemas are given as LIKE patterns, e.g. <c>dbo</c>, <c>wolverine%</c>, <c>queues</c>.
    /// </summary>
    public static async Task EmptySchemasAsync(string database, IReadOnlyList<string> schemaPatterns, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schemaPatterns);
        const string sql = """
            DECLARE @sql nvarchar(max) = N'';
            SELECT @sql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
            FROM sys.foreign_keys fk JOIN sys.tables t ON fk.parent_object_id = t.object_id JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE EXISTS (SELECT 1 FROM @patterns p WHERE s.name LIKE p.pattern);
            SELECT @sql += N'DROP TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';'
            FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE EXISTS (SELECT 1 FROM @patterns p WHERE s.name LIKE p.pattern);
            SELECT @sql += N'DROP PROCEDURE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(o.name) + N';'
            FROM sys.procedures o JOIN sys.schemas s ON o.schema_id = s.schema_id
            WHERE EXISTS (SELECT 1 FROM @patterns p WHERE s.name LIKE p.pattern);
            SELECT @sql += N'DROP TYPE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(ty.name) + N';'
            FROM sys.table_types ty JOIN sys.schemas s ON ty.schema_id = s.schema_id
            WHERE EXISTS (SELECT 1 FROM @patterns p WHERE s.name LIKE p.pattern);
            EXEC (@sql);
            """;
        var declarations = string.Join(" ", schemaPatterns.Select((_, i) => $"INSERT INTO @patterns VALUES (@p{i});"));
        await using var connection = new SqlConnection(TestSqlServer.ConnectionString(database));
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand($"DECLARE @patterns TABLE (pattern sysname); {declarations} {sql}", connection);
        for (var i = 0; i < schemaPatterns.Count; i++)
        {
            command.Parameters.AddWithValue($"@p{i}", schemaPatterns[i]);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

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
