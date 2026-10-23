using System.Diagnostics;
using System.Net;

namespace Flowza.Platform.IntegrationTests.Services;

/// <summary>
/// T033, PLT-03-AC2 (PL4): outside Development the DBA prepares every database with the services' own scripts, and
/// the services create nothing themselves. Runs in the service-databases group, which holds the shared test lock, so
/// no other test uses flowza_test_messaging meanwhile.
/// </summary>
[Collection(ServiceDatabasesTestGroup.Name)]
public sealed class ProductionDatabaseScriptsTests
{
    private const string MessagingDatabase = ServiceScripts.MessagingDatabase;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(ServiceApps.Names), MemberType = typeof(ServiceApps))]
    public async Task PLT_03_AC2_each_services_scripts_create_all_its_tables_and_can_run_twice(string service)
    {
        var scripts = await ServiceScripts.OfAsync(service);
        scripts.Select(s => s.FileName).ShouldBe([$"{service}.sql", $"{service}.messaging.sql"]);
        await ServiceScripts.EmptyDatabasesAsync(service, Ct);

        await ServiceScripts.RunAsync(scripts, service, Ct);
        await ServiceScripts.RunAsync(scripts, service, Ct); // the DBA may run them again: nothing fails, nothing changes

        var database = ServiceApps.DatabaseOf(service);
        (await ServiceScripts.TableExistsAsync(database, "dbo", "__EFMigrationsHistory", Ct)).ShouldBeTrue("the service's own tables (EF Core)");
        (await ServiceScripts.TableExistsAsync(database, "wolverine", "wolverine_incoming_envelopes", Ct)).ShouldBeTrue("the inbox");
        (await ServiceScripts.TableExistsAsync(database, "wolverine", "wolverine_outgoing_envelopes", Ct)).ShouldBeTrue("the outbox");
        (await ServiceScripts.TableExistsAsync(database, "wolverine", "wolverine_deduplication_hashed", Ct)).ShouldBeTrue("deduplication (MS3)");
        (await ServiceScripts.TableExistsAsync(MessagingDatabase, $"wolverine_{service}", "wolverine_nodes", Ct)).ShouldBeTrue("the service's node tables");
        (await ServiceScripts.TableExistsAsync(MessagingDatabase, "queues", $"wolverine_queue_{service}", Ct)).ShouldBeTrue("the service's own queue");
    }

    // Review finding 4: "runs twice" must also mean "keeps the data": a drop-then-create script would pass the test above.
    [Theory]
    [MemberData(nameof(ServiceApps.Names), MemberType = typeof(ServiceApps))]
    public async Task PLT_03_AC2_running_the_scripts_again_keeps_the_data(string service)
    {
        var scripts = await ServiceScripts.OfAsync(service);
        await ServiceScripts.EmptyDatabasesAsync(service, Ct);
        await ServiceScripts.RunAsync(scripts, service, Ct);
        var database = ServiceApps.DatabaseOf(service);
        var envelope = Guid.NewGuid();
        var queued = Guid.NewGuid();
        await ServiceScripts.ExecuteAsync(database, "INSERT INTO [__EFMigrationsHistory] VALUES (N'99999999999999_KeepMe', N'0')", Ct);
        await ServiceScripts.ExecuteAsync(
            database,
            "INSERT INTO wolverine.wolverine_incoming_envelopes (id, status, owner_id, body, message_type, received_at) VALUES (@id, 'Handled', 0, 0x00, 'test', 'test')",
            Ct,
            ("@id", envelope));
        await ServiceScripts.ExecuteAsync(
            MessagingDatabase,
            $"INSERT INTO queues.wolverine_queue_{service} (id, body, message_type) VALUES (@id, 0x00, 'test')",
            Ct,
            ("@id", queued));
        try
        {
            await ServiceScripts.RunAsync(scripts, service, Ct);

            (await ServiceScripts.CountAsync(database, "SELECT COUNT(*) FROM [__EFMigrationsHistory] WHERE MigrationId = N'99999999999999_KeepMe'", Ct)).ShouldBe(1);
            (await ServiceScripts.CountAsync(database, "SELECT COUNT(*) FROM wolverine.wolverine_incoming_envelopes WHERE id = @id", Ct, ("@id", envelope))).ShouldBe(1);
            (await ServiceScripts.CountAsync(MessagingDatabase, $"SELECT COUNT(*) FROM queues.wolverine_queue_{service} WHERE id = @id", Ct, ("@id", queued))).ShouldBe(1);
        }
        finally
        {
            await ServiceScripts.ExecuteAsync(database, "DELETE FROM [__EFMigrationsHistory] WHERE MigrationId = N'99999999999999_KeepMe'; DELETE FROM wolverine.wolverine_incoming_envelopes WHERE id = @id", Ct, ("@id", envelope));
            await ServiceScripts.ExecuteAsync(MessagingDatabase, $"DELETE FROM queues.wolverine_queue_{service} WHERE id = @id", Ct, ("@id", queued));
        }
    }

    // Review finding 4: the command the DBA really runs (Production, not started, no database, Identity without its
    // certificates) writes the same scripts as the tests above use.
    [Theory]
    [InlineData("identity", "Flowza.Identity.Api")]
    [InlineData("design", "Flowza.Design.Api")]
    [InlineData("runtime", "Flowza.Runtime.Api")]
    [InlineData("worker", "Flowza.Worker")]
    public async Task PLT_03_AC2_the_service_writes_its_scripts_from_the_command_line_without_a_database(string service, string program)
    {
        var expected = await ServiceScripts.OfAsync(service);
        var folder = Directory.CreateTempSubdirectory("flowza-scripts-").FullName;
        try
        {
            const string nowhere = "Server=tcp:127.0.0.1,1;Database=none;User Id=x;Password=y;TrustServerCertificate=true;Connect Timeout=1";
            var start = new ProcessStartInfo("dotnet", [Path.Combine(AppContext.BaseDirectory, $"{program}.dll"), "--database-scripts", folder])
            {
                WorkingDirectory = folder,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            start.Environment["DOTNET_ENVIRONMENT"] = "Production";
            start.Environment[$"ConnectionStrings__{service}-db"] = nowhere;
            start.Environment["ConnectionStrings__messaging-db"] = nowhere;
            start.Environment["Flowza__Identity__Authority"] = "https://identity.test/";
            start.Environment["Flowza__Logging__Folder"] = Path.Combine(folder, "logs");

            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(Ct);
            var errors = process.StandardError.ReadToEndAsync(Ct);
            await process.WaitForExitAsync(Ct);

            process.ExitCode.ShouldBe(0, await errors);
            foreach (var script in expected)
            {
                (await output).ShouldContain(script.FileName);
                (await File.ReadAllTextAsync(Path.Combine(folder, script.FileName), Ct)).ShouldBe(script.Sql, $"{script.FileName} as the DBA gets it");
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // Identity is not started in Production here: it also needs its token certificates in the machine's store.
    [Theory]
    [InlineData("design")]
    [InlineData("runtime")]
    [InlineData("worker")]
    public async Task PLT_03_AC2_a_service_starts_in_production_on_databases_prepared_only_by_its_scripts(string service)
    {
        var scripts = await ServiceScripts.OfAsync(service);
        await ServiceScripts.EmptyDatabasesAsync(service, Ct);
        await ServiceScripts.RunAsync(scripts, service, Ct);

        await using var app = ServiceApps.Create(service, "Production");
        using var client = app.CreateClient();
        using var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct);

        ready.StatusCode.ShouldBe(HttpStatusCode.OK, await ready.Content.ReadAsStringAsync(Ct));
        (await app.AppliedMigrationsAsync(Ct)).ShouldBe(app.AllMigrations(), "the script applied every migration");
    }

    [Fact]
    public async Task PL4_outside_development_a_service_creates_no_tables_itself()
    {
        var scripts = await ServiceScripts.OfAsync("worker");
        await ServiceScripts.EmptyDatabasesAsync("worker", Ct);
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

            (await ServiceScripts.CountTablesAsync("flowza_test_worker", "dbo", Ct)).ShouldBe(0, "no EF Core migration outside Development");
            (await ServiceScripts.CountTablesAsync("flowza_test_worker", "wolverine", Ct)).ShouldBe(0, "no outbox/inbox tables");
            (await ServiceScripts.CountTablesAsync(MessagingDatabase, "wolverine_worker", Ct)).ShouldBe(0, "no node tables");
            (await ServiceScripts.CountTablesAsync(MessagingDatabase, "queues", Ct)).ShouldBe(0, "no queue tables");
        }
        finally
        {
            await ServiceScripts.RunAsync(scripts, "worker", Ct); // leave the test databases usable for the next tests
        }
    }
}
