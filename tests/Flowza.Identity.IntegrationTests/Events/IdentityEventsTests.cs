using System.Net.Http.Json;
using System.Text.Json;
using Flowza.Contracts;
using Flowza.Contracts.Identity.Events.V1;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Organization;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.IntegrationTests.Connect;
using Flowza.Identity.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Tracking;

namespace Flowza.Identity.IntegrationTests.Events;

/// <summary>T017 / IAM-L5: important actions publish integration events through the outbox, with IDs only (plan §7).</summary>
[Collection(IdentityTestGroup.Name)]
public sealed class IdentityEventsTests(IdentityApp app) : IAsyncLifetime
{
    private static readonly string[] s_designer = ["AppDesigner"];
    private static readonly string[] s_admin = ["PlatformAdmin"];
    private static readonly string[] s_allowedFields =
    [
        "UserId", "Roles", "EventId", "OccurredAt", "ActorId", "CorrelationId",

        // Feature 002 (plan §6): the organization's IDs.
        "PrimaryUnitId", "ManagerId", "BusinessRoleIds", "UnitId", "ParentId", "UnitTypeId", "HeadUserId",
        "OldParentId", "NewParentId", "BusinessRoleId",
    ];

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
    public async Task T004_a_change_of_a_users_place_in_the_organization_publishes_its_event_through_the_outbox()
    {
        var user = await app.CreateUserAsync("placed.secret.u");
        var branchType = UnitType.Create(Guid.NewGuid(), "BRANCH", "Branch").Value;
        var role = BusinessRole.Create(Guid.NewGuid(), "REVIEWER", "Secret Reviewer").Value;
        OrgUnit branch = null!;
        await using (var setup = app.Services.CreateAsyncScope())
        {
            var services = setup.ServiceProvider;
            var root = (await services.GetRequiredService<IOrgUnitRepository>().GetAsync(OrgRoot.UnitId, Ct))!;
            var number = await services.GetRequiredService<IUnitNumberSource>().NextAsync(Ct);
            branch = OrgUnit.Create(Guid.NewGuid(), number, "BR-SECRET", "Secret Branch", branchType, root).Value;
            services.GetRequiredService<IUnitTypeRepository>().Add(branchType);
            services.GetRequiredService<IOrgUnitRepository>().Add(branch);
            services.GetRequiredService<IBusinessRoleRepository>().Add(role);
            (await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct)).IsSuccess.ShouldBeTrue();
        }

        var published = await PublishedWhileAsync(async () =>
        {
            await using var scope = app.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var loaded = (await users.GetAsync(user.Id, Ct))!;
            loaded.PlaceInUnit(branch).IsSuccess.ShouldBeTrue();
            loaded.AddBusinessRole(role).IsSuccess.ShouldBeTrue();
            (await unitOfWork.SaveChangesAsync(Ct)).IsSuccess.ShouldBeTrue();
        });

        var placed = published.OfType<UserOrgPlacementChanged>().ShouldHaveSingleItem();
        placed.UserId.ShouldBe(user.Id);
        placed.PrimaryUnitId.ShouldBe(branch.Id);
        placed.ManagerId.ShouldBeNull();
        published.OfType<UserBusinessRolesChanged>().ShouldHaveSingleItem().BusinessRoleIds.ShouldBe([role.Id]);
        foreach (var e in published)
        {
            AssertIdsOnly(e);
            e.ActorId.ShouldBeNull(); // no logged-in user outside a request: the system acted (T036); no trace either
        }
    }

    [Fact]
    public async Task T006_unit_type_changes_publish_their_events_through_the_outbox_with_the_admin_as_actor()
    {
        var (adminUser, admin) = await app.UserWithSessionAsync("types.events.admin", SystemRole.PlatformAdmin);
        using var _ = admin;
        Guid id = default;

        var created = await PublishedWhileAsync(async () =>
        {
            var response = await admin.Client.PostAsJsonAsync(new Uri("/api/v1/unit-types", UriKind.Relative), new { code = "ZONE", name = "Secret Zone" }, Ct);
            id = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        });
        created.OfType<UnitTypeCreated>().ShouldHaveSingleItem().UnitTypeId.ShouldBe(id);

        var renamed = await PublishedWhileAsync(async () => await admin.Client.PutAsJsonAsync(
            new Uri($"/api/v1/unit-types/{id}", UriKind.Relative), new { name = "Secret Area", version = await UnitTypeVersionAsync(admin, id) }, Ct));
        renamed.OfType<UnitTypeRenamed>().ShouldHaveSingleItem().UnitTypeId.ShouldBe(id);

        var deactivated = await PublishedWhileAsync(async () => await admin.Client.PostAsJsonAsync(
            new Uri($"/api/v1/unit-types/{id}/deactivate", UriKind.Relative), new { version = await UnitTypeVersionAsync(admin, id) }, Ct));
        deactivated.OfType<UnitTypeDeactivated>().ShouldHaveSingleItem().UnitTypeId.ShouldBe(id);

        var activated = await PublishedWhileAsync(async () => await admin.Client.PostAsJsonAsync(
            new Uri($"/api/v1/unit-types/{id}/activate", UriKind.Relative), new { version = await UnitTypeVersionAsync(admin, id) }, Ct));
        activated.OfType<UnitTypeActivated>().ShouldHaveSingleItem().UnitTypeId.ShouldBe(id);

        foreach (var published in created.Concat(renamed).Concat(deactivated).Concat(activated))
        {
            AssertIdsOnly(published);
            published.ActorId.ShouldBe(adminUser.Id);
            published.CorrelationId.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task T007_unit_changes_publish_their_events_through_the_outbox_with_ids_only()
    {
        var (adminUser, admin) = await app.UserWithSessionAsync("units.events.admin", SystemRole.PlatformAdmin);
        using var _ = admin;
        var head = await app.CreateUserAsync("head.secret.h");
        var type = await admin.Client.PostAsJsonAsync(new Uri("/api/v1/unit-types", UriKind.Relative), new { code = "ZONE", name = "Zone" }, Ct);
        var typeId = (await type.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        Guid id = default;

        var created = await PublishedWhileAsync(async () =>
        {
            var response = await admin.Client.PostAsJsonAsync(
                new Uri("/api/v1/org-units", UriKind.Relative),
                new { parentId = OrgRoot.UnitId, code = "SOUTH", name = "Secret South", unitTypeId = typeId },
                Ct);
            id = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        });
        var createdEvent = created.OfType<OrgUnitCreated>().ShouldHaveSingleItem();
        (createdEvent.UnitId, createdEvent.ParentId, createdEvent.UnitTypeId).ShouldBe((id, OrgRoot.UnitId, typeId));

        var renamed = await PublishedWhileAsync(async () => await admin.Client.PutAsJsonAsync(
            new Uri($"/api/v1/org-units/{id}", UriKind.Relative), new { name = "Secret Southern", version = await UnitVersionAsync(admin, id) }, Ct));
        renamed.OfType<OrgUnitRenamed>().ShouldHaveSingleItem().UnitId.ShouldBe(id);

        var headChanged = await PublishedWhileAsync(async () => await admin.Client.PutAsJsonAsync(
            new Uri($"/api/v1/org-units/{id}/head", UriKind.Relative), new { headUserId = head.Id, version = await UnitVersionAsync(admin, id) }, Ct));
        var headEvent = headChanged.OfType<OrgUnitHeadChanged>().ShouldHaveSingleItem();
        (headEvent.UnitId, headEvent.HeadUserId).ShouldBe((id, (Guid?)head.Id));

        foreach (var published in created.Concat(renamed).Concat(headChanged))
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

    /// <summary>
    /// The integration events the action <b>published</b>: sent to subscribers, or with no subscriber yet (most events
    /// wait for the Audit service). Received ones are left out: the service also listens to its own queue, so an event
    /// of an earlier test (another actor) can arrive during this one.
    /// </summary>
    private async Task<List<IntegrationEvent>> PublishedWhileAsync(Func<Task> action)
    {
        var session = await app.Services.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(30))
            .DoNotAssertOnExceptionsDetected()
            .ExecuteAndWaitAsync((Func<Wolverine.IMessageContext, Task>)(_ => action()));

        return [.. session.AllRecordsInOrder()
            .Where(record => record.MessageEventType is MessageEventType.Sent or MessageEventType.NoRoutes)
            .Select(record => record.Envelope?.Message)
            .OfType<IntegrationEvent>()
            .DistinctBy(e => e.EventId)];
    }

    private static async Task<string?> UnitTypeVersionAsync(ApiSession admin, Guid id)
    {
        var list = await admin.Client.GetFromJsonAsync<JsonElement>(new Uri("/api/v1/unit-types", UriKind.Relative), Ct);
        return list.EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == id).GetProperty("version").GetString();
    }

    private static async Task<string?> UnitVersionAsync(ApiSession admin, Guid id) =>
        (await admin.Client.GetFromJsonAsync<JsonElement>(new Uri($"/api/v1/org-units/{id}", UriKind.Relative), Ct)).GetProperty("version").GetString();

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
