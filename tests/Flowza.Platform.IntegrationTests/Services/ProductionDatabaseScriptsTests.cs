using System.Net;
using Flowza.Messaging;
using Flowza.Testing;
using Microsoft.Data.SqlClient;

namespace Flowza.Platform.IntegrationTests.Services;

/// <summary>
/// T033, PLT-03-AC2 (PL4): outside Development the DBA prepares every database with the services' own scripts, and
/// the services create nothing themselves. Runs in the service-databases group, which holds the shared test lock, so
/// no other test uses flowza_test_messaging meanwhile.
/// </summary>
[Collection(ServiceDatabasesTestGroup.Name)]
public sealed class ProductionDatabaseScriptsTests
{
    private const string MessagingDatabase = "flowza_test_messaging";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(ServiceApps.Names), MemberType = typeof(ServiceApps))]
    public async Task PLT_03_AC2_each_services_scripts_create_all_its_tables_and_can_run_twice(string service)
    {
        var scripts = await ScriptsOfAsync(service);
        scripts.Select(s => s.FileName).ShouldBe([$"{service}.sql", $"{service}.messaging.sql"]);
        await EmptyDatabasesAsync(service);

        await RunAsync(scripts, service);
        await RunAsync(scripts, service); // the DBA may run them again: nothing fails, nothing changes

        var database = ServiceApps.DatabaseOf(service);
        (await TableExistsAsync(database, "dbo", "__EFMigrationsHistory")).ShouldBeTrue("the service's own tables (EF Core)");
        (await TableExistsAsync(database, "wolverine", "wolverine_incoming_envelopes")).ShouldBeTrue("the inbox");
        (await TableExistsAsync(database, "wolverine", "wolverine_outgoing_envelopes")).ShouldBeTrue("the outbox");
        (await TableExistsAsync(database, "wolverine", "wolverine_deduplication_hashed")).ShouldBeTrue("deduplication (MS3)");
        (await TableExistsAsync(MessagingDatabase, $"wolverine_{service}", "wolverine_nodes")).ShouldBeTrue("the service's node tables");
        (await TableExistsAsync(MessagingDatabase, "queues", $"wolverine_queue_{service}")).ShouldBeTrue("the service's own queue");
    }

    // Identity is not started in Production here: it also needs its token certificates in the machine's store.
    [Theory]
    [InlineData("design")]
    [InlineData("runtime")]
    [InlineData("worker")]
    public async Task PLT_03_AC2_a_service_starts_in_production_on_databases_prepared_only_by_its_scripts(string service)
    {
        var scripts = await ScriptsOfAsync(service);
        await EmptyDatabasesAsync(service);
        await RunAsync(scripts, service);

        await using var app = ServiceApps.Create(service, "Production");
        using var client = app.CreateClient();
        using var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct);

        ready.StatusCode.ShouldBe(HttpStatusCode.OK, await ready.Content.ReadAsStringAsync(Ct));
        (await app.AppliedMigrationsAsync(Ct)).ShouldBe(app.AllMigrations(), "the script applied every migration");
    }

    [Fact]
    public async Task PL4_outside_development_a_service_creates_no_tables_itself()
    {
        var scripts = await ScriptsOfAsync("worker");
        await EmptyDatabasesAsync("worker");
        try
        {
            await using (var app = ServiceApps.Create("worker", "Production"))
            {
                // Starting may fail on the missing tables or succeed and wait; either way nothing may be created.
                try
                {
                    using var client = app.CreateClient();
                    using var _ = await client.GetAsync(new Uri("/health/live", UriKind.Relative), Ct);
                }
                catch (Exception error) when (error is not ShouldAssertException)
                {
                    // expected when Wolverine refuses to start without its tables
                }
            }

            (await CountTablesAsync("flowza_test_worker", "dbo")).ShouldBe(0, "no EF Core migration outside Development");
            (await CountTablesAsync("flowza_test_worker", "wolverine")).ShouldBe(0, "no outbox/inbox tables");
            (await CountTablesAsync(MessagingDatabase, "wolverine_worker")).ShouldBe(0, "no node tables");
            (await CountTablesAsync(MessagingDatabase, "queues")).ShouldBe(0, "no queue tables");
        }
        finally
        {
            await RunAsync(scripts, "worker"); // leave the test databases usable for the next tests
        }
    }

    /// <summary>The scripts as the service writes them (from a Development start, which builds the same model).</summary>
    private static async Task<IReadOnlyList<DatabaseScript>> ScriptsOfAsync(string service)
    {
        await using var app = ServiceApps.Create(service);
        using var _ = app.CreateClient();
        return await app.ProductionScriptsAsync();
    }

    /// <summary>The service's database and its part of the messaging database, as the DBA receives them.</summary>
    private static async Task EmptyDatabasesAsync(string service)
    {
        await TestDatabase.EmptySchemasAsync(ServiceApps.DatabaseOf(service), ["dbo", "wolverine"], Ct);
        await TestDatabase.EmptySchemasAsync(MessagingDatabase, [$"wolverine_{service}", "queues"], Ct);
    }

    private static async Task RunAsync(IReadOnlyList<DatabaseScript> scripts, string service)
    {
        foreach (var script in scripts)
        {
            var database = script.Database == "messaging" ? MessagingDatabase : ServiceApps.DatabaseOf(service);
            await using var connection = new SqlConnection(TestSqlServer.ConnectionString(database));
            await connection.OpenAsync(Ct);
            foreach (var batch in SqlBatches.Split(script.Sql))
            {
                await using var command = new SqlCommand(batch, connection);
                await command.ExecuteNonQueryAsync(Ct);
            }
        }
    }

    private static async Task<bool> TableExistsAsync(string database, string schema, string table) =>
        await ScalarAsync(database, "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = @schema AND t.name = @table", schema, table) == 1;

    private static Task<int> CountTablesAsync(string database, string schema) =>
        ScalarAsync(database, "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = @schema", schema, null);

    private static async Task<int> ScalarAsync(string database, string sql, string schema, string? table)
    {
        await using var connection = new SqlConnection(TestSqlServer.ConnectionString(database));
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@table", (object?)table ?? DBNull.Value);
        return (int)(await command.ExecuteScalarAsync(Ct))!;
    }
}
