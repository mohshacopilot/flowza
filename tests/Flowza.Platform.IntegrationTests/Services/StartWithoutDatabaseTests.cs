using System.Net;
using Flowza.ServiceDefaults.Hosting;
using Flowza.Testing;

namespace Flowza.Platform.IntegrationTests.Services;

/// <summary>
/// T034, spec 001 edge case and PLT-05: a service started while its database (and the message transport, which lives
/// in SQL Server too) cannot be reached starts anyway, reports "not ready", and becomes ready by itself once the
/// database is back, having run its start-up steps (migration, messaging, deny list).
/// </summary>
[Collection(ServiceDatabasesTestGroup.Name)]
public sealed class StartWithoutDatabaseTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(ServiceApps.Names), MemberType = typeof(ServiceApps))]
    public async Task PLT_05_a_service_started_without_its_database_is_live_not_ready_and_recovers_by_itself(string service)
    {
        // A new installation's database without tables: the Development migration must run after the database returns.
        await TestDatabase.DropServiceTablesAsync(ServiceApps.DatabaseOf(service), Ct);
        await using var relay = TcpRelay.ForTestSqlServer();
        await using var app = ServiceApps.Create(
            service,
            connect: database => TestSqlServer.ConnectionStringVia(database, relay.Port),
            settings: new Dictionary<string, string?> { [StartupStepRunner.MaxRetryDelayKey] = "00:00:02" });

        using var client = app.CreateClient(); // starts the service: must not fail while the database is down

        (await client.GetAsync(new Uri("/health/live", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using (var notReady = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct))
        {
            notReady.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await notReady.Content.ReadAsStringAsync(Ct)).ShouldContain("\"startup\":{\"status\":\"Unhealthy\"");
        }

        relay.IsOpen = true; // the database is back

        var ready = await WaitUntilReadyAsync(client, TimeSpan.FromSeconds(60));
        ready.ShouldBeTrue("the service becomes ready by itself once its database answers");
        (await app.AppliedMigrationsAsync(Ct)).ShouldBe(app.AllMigrations(), "the migration ran after the database returned");
    }

    private static async Task<bool> WaitUntilReadyAsync(HttpClient client, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), Ct);
        }

        return false;
    }
}
