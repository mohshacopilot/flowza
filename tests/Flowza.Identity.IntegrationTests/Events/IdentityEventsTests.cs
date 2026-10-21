using System.Net.Http.Json;
using System.Text.Json;
using Flowza.Contracts;
using Flowza.Contracts.Identity.Events.V1;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.IntegrationTests.Connect;
using Flowza.Identity.IntegrationTests.Infrastructure;
using Wolverine.Tracking;

namespace Flowza.Identity.IntegrationTests.Events;

/// <summary>T017 / IAM-L5: important actions publish integration events through the outbox, with IDs only (plan §7).</summary>
[Collection(IdentityTestGroup.Name)]
public sealed class IdentityEventsTests(IdentityApp app) : IAsyncLifetime
{
    private static readonly string[] s_designer = ["AppDesigner"];
    private static readonly string[] s_admin = ["PlatformAdmin"];
    private static readonly string[] s_allowedFields = ["UserId", "Roles", "EventId", "OccurredAt", "ActorId", "CorrelationId"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await app.ResetDataAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task IAM_L5_AC1_login_failed_login_lockout_and_password_change_each_publish_an_event_with_ids_only()
    {
        var user = await app.CreateUserAsync("ravi.secret.k");
        using var browser = app.CreateBrowser();

        var loggedIn = await PublishedWhileAsync(() => LoginBrowser.PostLoginAsync(browser, "ravi.secret.k", IdentityAppUsers.Password, null, Ct));
        loggedIn.OfType<UserLoggedIn>().ShouldHaveSingleItem().UserId.ShouldBe(user.Id);

        var failed = await PublishedWhileAsync(() => LoginBrowser.PostLoginAsync(browser, "ravi.secret.k", "Wrong-Password-1", null, Ct));
        failed.OfType<UserLoginFailed>().ShouldHaveSingleItem().UserId.ShouldBe(user.Id);

        var lockout = await PublishedWhileAsync(async () =>
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                await LoginBrowser.PostLoginAsync(browser, "ravi.secret.k", "Wrong-Password-1", null, Ct);
            }
        });
        lockout.OfType<UserLockedOut>().ShouldHaveSingleItem().UserId.ShouldBe(user.Id);
        lockout.OfType<UserLoginFailed>().Count().ShouldBe(4);

        var other = await app.CreateUserAsync("asha.secret.n");
        using var session = await app.LoginAsync("asha.secret.n", IdentityAppUsers.Password);
        var changed = await PublishedWhileAsync(() => session.Client.PostAsJsonAsync(
            new Uri("/api/v1/me/password", UriKind.Relative),
            new { currentPassword = IdentityAppUsers.Password, newPassword = "Another-Strong-Pass-3" },
            Ct));
        changed.OfType<UserPasswordChanged>().ShouldHaveSingleItem().UserId.ShouldBe(other.Id);

        foreach (var published in loggedIn.Concat(failed).Concat(lockout).Concat(changed))
        {
            AssertIdsOnly(published);
            published.OccurredAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
            published.CorrelationId.ShouldNotBeNullOrEmpty();
        }

        // Before login nobody is logged in: the user is the actor of their own login events.
        foreach (var published in loggedIn.Concat(failed).Concat(lockout))
        {
            published.ActorId.ShouldBe(user.Id);
        }

        // T036: through the API, the actor is the logged-in user.
        changed.ShouldAllBe(e => e.ActorId == other.Id);
    }

    [Fact]
    public async Task IAM_L5_AC1_a_login_with_an_unknown_username_publishes_nothing()
    {
        using var browser = app.CreateBrowser();

        var published = await PublishedWhileAsync(() => LoginBrowser.PostLoginAsync(browser, "nobody.at.all", "Wrong-Password-1", null, Ct));

        published.ShouldBeEmpty();
    }

    [Fact]
    public async Task User_administration_publishes_its_events_with_ids_only_and_the_admin_as_actor()
    {
        var (adminUser, admin) = await app.UserWithSessionAsync("events.admin", SystemRole.PlatformAdmin);
        using var _ = admin;
        Guid id = default;

        var created = await PublishedWhileAsync(async () =>
        {
            var response = await admin.Client.PostAsJsonAsync(
                new Uri("/api/v1/users", UriKind.Relative),
                new { username = "meena.secret.r", displayName = "Meena Secret", email = "meena.secret@example.org", roles = s_designer },
                Ct);
            id = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        });
        created.OfType<UserCreated>().ShouldHaveSingleItem().UserId.ShouldBe(id);

        var roles = await PublishedWhileAsync(() => admin.Client.PutAsJsonAsync(
            new Uri($"/api/v1/users/{id}/roles", UriKind.Relative), new { roles = s_admin }, Ct));
        roles.OfType<UserRolesChanged>().ShouldHaveSingleItem().Roles.ShouldBe(["PlatformAdmin", "User"], ignoreOrder: true);

        var reset = await PublishedWhileAsync(() => admin.Client.PostAsync(new Uri($"/api/v1/users/{id}/reset-password", UriKind.Relative), null, Ct));
        reset.OfType<UserPasswordReset>().ShouldHaveSingleItem().UserId.ShouldBe(id);

        var deactivated = await PublishedWhileAsync(() => admin.Client.PostAsync(new Uri($"/api/v1/users/{id}/deactivate", UriKind.Relative), null, Ct));
        deactivated.OfType<UserDeactivated>().ShouldHaveSingleItem().UserId.ShouldBe(id);

        var activated = await PublishedWhileAsync(() => admin.Client.PostAsync(new Uri($"/api/v1/users/{id}/activate", UriKind.Relative), null, Ct));
        activated.OfType<UserActivated>().ShouldHaveSingleItem().UserId.ShouldBe(id);

        foreach (var published in created.Concat(roles).Concat(reset).Concat(deactivated).Concat(activated))
        {
            AssertIdsOnly(published);
            published.ActorId.ShouldBe(adminUser.Id);
        }
    }

    [Fact]
    public async Task A_refused_change_publishes_nothing()
    {
        var (adminUser, admin) = await app.UserWithSessionAsync("only.admin", SystemRole.PlatformAdmin);
        using var _ = admin;

        var published = await PublishedWhileAsync(() => admin.Client.PostAsync(new Uri($"/api/v1/users/{adminUser.Id}/deactivate", UriKind.Relative), null, Ct));

        published.ShouldBeEmpty();
    }

    private async Task<List<IntegrationEvent>> PublishedWhileAsync(Func<Task> action)
    {
        var session = await app.Services.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(30))
            .DoNotAssertOnExceptionsDetected()
            .ExecuteAndWaitAsync((Func<Wolverine.IMessageContext, Task>)(_ => action()));

        return [.. session.AllRecordsInOrder()
            .Select(record => record.Envelope?.Message)
            .OfType<IntegrationEvent>()
            .DistinctBy(e => e.EventId)];
    }

    private static void AssertIdsOnly(IntegrationEvent published)
    {
        var json = JsonSerializer.Serialize<object>(published);
        foreach (var property in JsonDocument.Parse(json).RootElement.EnumerateObject())
        {
            property.Name.ShouldBeOneOf(s_allowedFields, $"{published.GetType().Name} must carry IDs only");
        }

        json.ShouldNotContain("secret", Case.Insensitive);
        json.ShouldNotContain("@");
    }
}
