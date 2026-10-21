using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.IntegrationTests.Infrastructure;
using Flowza.Persistence;
using Flowza.ServiceDefaults.Security;
using Flowza.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowza.Identity.IntegrationTests.Users;

/// <summary>T018 (deny list, PA4) and T036 (the logged-in user in audit columns) on the real service.</summary>
[Collection(IdentityTestGroup.Name)]
public sealed class DenyListAndAuditTests(IdentityApp app) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ApiSession _admin = null!;
    private User _adminUser = null!;

    public async ValueTask InitializeAsync()
    {
        await app.ResetDataAsync();
        (_adminUser, _admin) = await app.UserWithSessionAsync("deny.admin", SystemRole.PlatformAdmin);
    }

    public ValueTask DisposeAsync()
    {
        _admin.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <remarks>
    /// Runs on RabbitMQ: with the SQL Server transport, Wolverine keeps each service's queue tables in that service's
    /// own database, so events cannot reach another service yet (reported to the main session; see tasks.md T018).
    /// </remarks>
    [Fact]
    public async Task IAM_04_AC3_the_UserDeactivated_event_reaches_the_services_deny_list_through_RabbitMQ()
    {
        Environment.SetEnvironmentVariable("Messaging__Transport", "RabbitMq");
        Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", TestRabbitMq.ConnectionString());
        try
        {
            await using var rabbit = new IdentityApp();
            var (_, admin) = await rabbit.UserWithSessionAsync("rabbit.admin", SystemRole.PlatformAdmin);
            using var _ = admin;
            var user = await rabbit.CreateUserAsync("deny.me");
            var denyList = rabbit.Services.GetRequiredService<IUserDenyList>();

            await admin.Client.PostAsync(new Uri($"/api/v1/users/{user.Id}/deactivate", UriKind.Relative), null, Ct);
            (await WaitUntilAsync(() => denyList.IsDenied(user.Id))).ShouldBeTrue("UserDeactivated should arrive through the identity queue");

            await admin.Client.PostAsync(new Uri($"/api/v1/users/{user.Id}/activate", UriKind.Relative), null, Ct);
            (await WaitUntilAsync(() => !denyList.IsDenied(user.Id))).ShouldBeTrue("UserActivated should arrive too");
        }
        finally
        {
            Environment.SetEnvironmentVariable("Messaging__Transport", null);
            Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", null);
        }
    }

    [Fact]
    public async Task IAM_04_AC3_a_valid_token_of_a_user_on_the_deny_list_is_refused_with_401()
    {
        var (user, session) = await app.UserWithSessionAsync("still.valid");
        using var _ = session;
        var info = new Uri("/api/v1/info", UriKind.Relative);
        (await session.Client.GetAsync(info, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var denyList = app.Services.GetRequiredService<IUserDenyList>();
        denyList.Deny(user.Id, DateTimeOffset.UtcNow);
        try
        {
            (await session.Client.GetAsync(info, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
        finally
        {
            denyList.Allow(user.Id, DateTimeOffset.UtcNow.AddSeconds(1));
        }

        (await session.Client.GetAsync(info, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PA4_the_deny_list_is_loaded_at_start_up()
    {
        var user = await app.CreateUserAsync("was.deactivated");
        await _admin.Client.PostAsync(new Uri($"/api/v1/users/{user.Id}/deactivate", UriKind.Relative), null, Ct);

        // A fresh instance of the service (a restart) knows the inactive user before any event arrives.
        await using var restarted = new IdentityApp();
        restarted.Services.GetRequiredService<IUserDenyList>().IsDenied(user.Id).ShouldBeTrue();
        restarted.Services.GetRequiredService<IUserDenyList>().IsDenied(_adminUser.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task T036_audit_columns_record_the_logged_in_admin_not_the_system()
    {
        var created = await _admin.Client.PostAsJsonAsync(
            new Uri("/api/v1/users", UriKind.Relative),
            new { username = "audited.one", displayName = "Audited", email = "audited.one@example.org" },
            Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

        await _admin.Client.PostAsync(new Uri($"/api/v1/users/{id}/deactivate", UriKind.Relative), null, Ct);

        var audit = await app.WithDbContextAsync(context => context.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new
            {
                CreatedBy = EF.Property<Guid?>(u, ShadowColumns.CreatedBy),
                UpdatedBy = EF.Property<Guid?>(u, ShadowColumns.UpdatedBy),
            })
            .SingleAsync(Ct));
        audit.CreatedBy.ShouldBe(_adminUser.Id);
        audit.UpdatedBy.ShouldBe(_adminUser.Id);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(100, Ct);
        }

        return condition();
    }
}
