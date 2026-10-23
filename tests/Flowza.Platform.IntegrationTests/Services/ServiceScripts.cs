using Flowza.Messaging;
using Flowza.Testing;
using Microsoft.Data.SqlClient;

namespace Flowza.Platform.IntegrationTests.Services;

/// <summary>The production database scripts of a service, and the test databases they run on (T033, T034).</summary>
internal static class ServiceScripts
{
    public const string MessagingDatabase = "flowza_test_messaging";

    /// <summary>The scripts as the service writes them (from a Development start, which builds the same model).</summary>
    public static async Task<IReadOnlyList<DatabaseScript>> OfAsync(string service)
    {
        await using var app = ServiceApps.Create(service);
        using var _ = app.CreateClient();
        return await app.ProductionScriptsAsync();
    }

    /// <summary>The service's database and its part of the messaging database, as the DBA receives them.</summary>
    public static async Task EmptyDatabasesAsync(string service, CancellationToken cancellationToken)
    {
        await TestDatabase.EmptySchemasAsync(ServiceApps.DatabaseOf(service), ["dbo", "wolverine"], cancellationToken);
        await TestDatabase.EmptySchemasAsync(MessagingDatabase, [$"wolverine_{service}", "queues"], cancellationToken);
    }

    public static string DatabaseOf(DatabaseScript script, string service) =>
        script.Database == "messaging" ? MessagingDatabase : ServiceApps.DatabaseOf(service);

    public static async Task RunAsync(IEnumerable<DatabaseScript> scripts, string service, CancellationToken cancellationToken)
    {
        foreach (var script in scripts)
        {
            await using var connection = new SqlConnection(TestSqlServer.ConnectionString(DatabaseOf(script, service)));
            await connection.OpenAsync(cancellationToken);
            foreach (var batch in SqlBatches.Split(script.Sql))
            {
                await using var command = new SqlCommand(batch, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    public static async Task<int> ExecuteAsync(string database, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new SqlConnection(TestSqlServer.ConnectionString(database));
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<int> CountAsync(string database, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new SqlConnection(TestSqlServer.ConnectionString(database));
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public static async Task<bool> TableExistsAsync(string database, string schema, string table, CancellationToken cancellationToken) =>
        await CountAsync(
            database,
            "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = @schema AND t.name = @table",
            cancellationToken,
            ("@schema", schema),
            ("@table", table)) == 1;

    public static Task<int> CountTablesAsync(string database, string schema, CancellationToken cancellationToken) =>
        CountAsync(
            database,
            "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = @schema",
            cancellationToken,
            ("@schema", schema));
}
