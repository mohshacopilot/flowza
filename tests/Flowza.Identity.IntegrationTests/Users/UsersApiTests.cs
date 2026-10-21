using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.IntegrationTests.Connect;
using Flowza.Identity.IntegrationTests.Infrastructure;

namespace Flowza.Identity.IntegrationTests.Users;

/// <summary>T015: the Users API for Platform Admins (plan 001 §5.3), on the real service and database.</summary>
[Collection(IdentityTestGroup.Name)]
public sealed class UsersApiTests(IdentityApp app) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ApiSession _admin = null!;
    private User _adminUser = null!;

    public async ValueTask InitializeAsync()
    {
        await app.ResetDataAsync();
        (_adminUser, _admin) = await app.UserWithSessionAsync("main.admin", SystemRole.PlatformAdmin);
    }

    public ValueTask DisposeAsync()
    {
        _admin.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task IAM_02_AC1_user_APIs_need_Platform_Admin_and_refuse_anonymous_callers()
    {
        var (_, designer) = await app.UserWithSessionAsync("des.igner", SystemRole.AppDesigner);
        using var anonymous = app.CreateClient();

        (await anonymous.GetAsync(Users(), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await designer.Client.GetAsync(Users(), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await designer.Client.PostAsJsonAsync(Users(), NewUser("x.y.z"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _admin.Client.GetAsync(Users(), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        designer.Dispose();
    }

    [Fact]
    public async Task IAM_04_AC1_the_admin_creates_reads_edits_deactivates_and_activates_a_user_and_the_username_never_changes()
    {
        var created = await _admin.Client.PostAsJsonAsync(Users(), NewUser("ravi.k", "AppDesigner"), Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        created.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = body.GetProperty("id").GetGuid();
        body.GetProperty("temporaryPassword").GetString()!.Length.ShouldBe(16);
        created.Headers.Location!.ToString().ShouldBe($"/api/v1/users/{id}");

        var details = await GetUserAsync(id);
        details.GetProperty("username").GetString().ShouldBe("ravi.k");
        details.GetProperty("status").GetString().ShouldBe("Active");
        details.GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
        details.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["AppDesigner", "User"], ignoreOrder: true);

        // An attempt to send a new username is ignored: the contract has no such field (C4).
        var update = await _admin.Client.PutAsJsonAsync(User(id), new
        {
            username = "someone.else",
            displayName = "Ravi Kumar",
            email = "ravi.kumar@bank.example",
            mobile = "+91 98765 43210",
            version = details.GetProperty("version").GetString(),
        }, Ct);
        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var edited = await GetUserAsync(id);
        edited.GetProperty("username").GetString().ShouldBe("ravi.k");
        edited.GetProperty("displayName").GetString().ShouldBe("Ravi Kumar");
        edited.GetProperty("email").GetString().ShouldBe("ravi.kumar@bank.example");

        (await _admin.Client.PostAsync(new Uri($"{User(id)}/deactivate", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetUserAsync(id)).GetProperty("status").GetString().ShouldBe("Inactive");
        (await _admin.Client.PostAsync(new Uri($"{User(id)}/activate", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetUserAsync(id)).GetProperty("status").GetString().ShouldBe("Active");
    }

    [Fact]
    public async Task Constitution_8_5_editing_with_an_old_version_is_a_conflict()
    {
        var id = await CreateAsync("meena.r");
        var version = (await GetUserAsync(id)).GetProperty("version").GetString();

        (await _admin.Client.PutAsJsonAsync(User(id), new { displayName = "First", email = "meena.r@example.org", version }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var second = await _admin.Client.PutAsJsonAsync(User(id), new { displayName = "Second", email = "meena.r@example.org", version }, Ct);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(second)).ShouldBe("common.changed_by_someone_else");
        (await GetUserAsync(id)).GetProperty("displayName").GetString().ShouldBe("First");
    }

    [Fact]
    public async Task IAM_04_AC2_a_reset_shows_a_temporary_password_once_and_the_user_must_change_it()
    {
        var id = await CreateAsync("asha.n");

        var reset = await _admin.Client.PostAsync(new Uri($"{User(id)}/reset-password", UriKind.Relative), null, Ct);
        reset.StatusCode.ShouldBe(HttpStatusCode.OK);
        reset.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var temporary = (await reset.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("password").GetString()!;

        var details = await _admin.Client.GetStringAsync(User(id), Ct);
        details.ShouldNotContain(temporary);
        details.ShouldContain("\"mustChangePassword\":true");

        using var browser = app.CreateBrowser();
        var login = await LoginBrowser.PostLoginAsync(browser, "asha.n", temporary, "/", Ct);
        login.Headers.Location!.ToString().ShouldStartWith("/account/change-password");
    }

    [Fact]
    public async Task IAM_04_AC3_deactivation_ends_the_users_tokens_at_once()
    {
        var (user, session) = await app.UserWithSessionAsync("gone.soon");
        using var _ = session;
        (await session.Client.GetAsync(new Uri("/api/v1/info", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await _admin.Client.PostAsync(new Uri($"{User(user.Id)}/deactivate", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The next action is refused, and no new tokens can be had (K7). Other services refuse through the deny list (T018).
        (await session.Client.GetAsync(new Uri("/api/v1/info", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var client = app.CreateClient();
        (await OidcClient.RefreshAsync(client, session.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("MAIN.ADMIN", "new@example.org", "users.username_taken")]
    [InlineData("other.user", "MAIN.ADMIN@EXAMPLE.ORG", "users.email_taken")]
    [InlineData("no", "new@example.org", "users.username_invalid")]
    [InlineData("has space", "new@example.org", "users.username_invalid")]
    [InlineData("good.name", "not-an-email", "users.email_invalid")]
    public async Task IAM_04_AC4_bad_or_already_used_usernames_and_emails_are_rejected_ignoring_case(string username, string email, string code)
    {
        var response = await _admin.Client.PostAsJsonAsync(Users(), new { username, displayName = "Someone", email, roles = Array.Empty<string>() }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(response)).ShouldBe(code);
    }

    [Fact]
    public async Task IAM_04_AC4_an_edit_to_an_email_that_another_user_has_is_rejected()
    {
        var id = await CreateAsync("priya.s");
        var version = (await GetUserAsync(id)).GetProperty("version").GetString();

        var response = await _admin.Client.PutAsJsonAsync(User(id), new { displayName = "Priya", email = "Main.Admin@Example.org", version }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(response)).ShouldBe("users.email_taken");
    }

    [Fact]
    public async Task IAM_04_AC5_a_reactivated_user_logs_in_again_with_roles_and_history_unchanged()
    {
        var user = await app.CreateUserAsync("back.again", roles: SystemRole.AppDesigner);
        var before = await GetUserAsync(user.Id);
        await _admin.Client.PostAsync(new Uri($"{User(user.Id)}/deactivate", UriKind.Relative), null, Ct);

        using (var browser = app.CreateBrowser())
        {
            var refused = await LoginBrowser.PostLoginAsync(browser, "back.again", IdentityAppUsers.Password, null, Ct);
            LoginBrowser.AlertText(await refused.Content.ReadAsStringAsync(Ct)).ShouldBe("Username or password is incorrect.");
        }

        await _admin.Client.PostAsync(new Uri($"{User(user.Id)}/activate", UriKind.Relative), null, Ct);

        using var session = await app.LoginAsync("back.again", IdentityAppUsers.Password);
        var after = await GetUserAsync(user.Id);
        after.GetProperty("roles").ToString().ShouldBe(before.GetProperty("roles").ToString());
        after.GetProperty("displayName").GetString().ShouldBe(before.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task IAM_02_AC2_several_roles_can_be_set_and_the_last_active_admin_is_protected()
    {
        var id = await CreateAsync("multi.role");
        (await _admin.Client.PutAsJsonAsync($"{User(id)}/roles", RolesBody("AppDesigner", "PlatformAdmin"), Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetUserAsync(id)).GetProperty("roles").EnumerateArray().Select(r => r.GetString())
            .ShouldBe(["PlatformAdmin", "AppDesigner", "User"], ignoreOrder: true);

        // Two admins now: one may lose the role. Then the remaining one is the last and is protected.
        (await _admin.Client.PutAsJsonAsync($"{User(id)}/roles", RolesBody("User"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var removeOwn = await _admin.Client.PutAsJsonAsync($"{User(_adminUser.Id)}/roles", RolesBody("User"), Ct);
        removeOwn.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(removeOwn)).ShouldBe("users.last_active_platform_admin");

        var deactivateLast = await _admin.Client.PostAsync(new Uri($"{User(_adminUser.Id)}/deactivate", UriKind.Relative), null, Ct);
        deactivateLast.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(deactivateLast)).ShouldBe("users.last_active_platform_admin");
    }

    [Fact]
    public async Task An_unknown_role_name_is_rejected()
    {
        var response = await _admin.Client.PostAsJsonAsync(Users(), NewUser("role.typo", "SuperUser"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(response)).ShouldBe("users.role_unknown");
    }

    [Fact]
    public async Task IAM_07_AC1_the_admin_unlocks_a_locked_account_through_the_API()
    {
        var user = await app.CreateUserAsync("locked.out");
        using (var browser = app.CreateBrowser())
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                await LoginBrowser.PostLoginAsync(browser, "locked.out", "Wrong-Password-1", null, Ct);
            }
        }

        (await GetUserAsync(user.Id)).GetProperty("status").GetString().ShouldBe("Locked");
        (await _admin.Client.PostAsync(new Uri($"{User(user.Id)}/unlock", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetUserAsync(user.Id)).GetProperty("status").GetString().ShouldBe("Active");
    }

    [Fact]
    public async Task The_list_searches_filters_by_status_and_pages_with_a_total()
    {
        foreach (var name in new[] { "list.a", "list.b", "list.c", "other.d" })
        {
            await CreateAsync(name);
        }

        var inactive = await CreateAsync("list.e");
        await _admin.Client.PostAsync(new Uri($"{User(inactive)}/deactivate", UriKind.Relative), null, Ct);

        var page = await _admin.Client.GetFromJsonAsync<JsonElement>(new Uri("/api/v1/users?search=LIST.&size=2&page=1", UriKind.Relative), Ct);
        page.GetProperty("totalCount").GetInt32().ShouldBe(4);
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("username").GetString()).ShouldBe(["list.a", "list.b"]);
        page.GetProperty("totalPages").GetInt32().ShouldBe(2);

        var inactiveOnly = await _admin.Client.GetFromJsonAsync<JsonElement>(new Uri("/api/v1/users?status=inactive", UriKind.Relative), Ct);
        inactiveOnly.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("username").GetString()).ShouldBe(["list.e"]);

        (await _admin.Client.GetAsync(new Uri("/api/v1/users?status=sleeping", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_user_is_not_found()
    {
        var response = await _admin.Client.GetAsync(User(Guid.NewGuid()), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ProblemCodeAsync(response)).ShouldBe("users.not_found");
    }

    private static object RolesBody(params string[] roles) => new { roles };

    private static Uri Users() => new("/api/v1/users", UriKind.Relative);

    private static Uri User(Guid id) => new($"/api/v1/users/{id}", UriKind.Relative);

    private static object NewUser(string username, params string[] roles) =>
        new { username, displayName = $"Test {username}", email = $"{username}@example.org", roles };

    private async Task<Guid> CreateAsync(string username)
    {
        var response = await _admin.Client.PostAsJsonAsync(Users(), NewUser(username), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private Task<JsonElement> GetUserAsync(Guid id) => _admin.Client.GetFromJsonAsync<JsonElement>(User(id), Ct);

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
