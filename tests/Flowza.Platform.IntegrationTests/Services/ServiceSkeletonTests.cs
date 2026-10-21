using System.Net;

namespace Flowza.Platform.IntegrationTests.Services;

/// <summary>Every service starts, owns its database, reports health and refuses anonymous API calls (T007).</summary>
public sealed class ServiceSkeletonTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(ServiceApps.Names), MemberType = typeof(ServiceApps))]
    public async Task IAM_05_AC1_anonymous_api_calls_are_refused(string service)
    {
        await using var app = ServiceApps.Create(service);
        using var client = app.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/info", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(ServiceApps.Names), MemberType = typeof(ServiceApps))]
    public async Task IAM_05_AC1_health_checks_need_no_login_and_PLT_05_AC1_ready_checks_the_database(string service)
    {
        await using var app = ServiceApps.Create(service);
        using var client = app.CreateClient();

        (await client.GetAsync(new Uri("/health/live", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct);
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ready.Content.ReadAsStringAsync(Ct)).ShouldContain("\"database\"");
    }

    [Theory]
    [MemberData(nameof(ServiceApps.Names), MemberType = typeof(ServiceApps))]
    public async Task PLT_03_AC1_first_start_applies_the_migrations_and_a_restart_changes_nothing(string service)
    {
        IReadOnlyList<string> afterFirstStart;
        await using (var first = ServiceApps.Create(service))
        {
            using var client = first.CreateClient();
            afterFirstStart = await first.AppliedMigrationsAsync(Ct);
        }

        await using var second = ServiceApps.Create(service);
        using var secondClient = second.CreateClient();
        var afterRestart = await second.AppliedMigrationsAsync(Ct);

        afterFirstStart.ShouldNotBeEmpty();
        afterFirstStart.ShouldContain(m => m.EndsWith("_Initial", StringComparison.Ordinal));
        afterRestart.ShouldBe(afterFirstStart);
    }
}
