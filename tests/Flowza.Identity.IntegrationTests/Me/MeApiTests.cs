using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.IntegrationTests.Connect;
using Flowza.Identity.IntegrationTests.Infrastructure;

namespace Flowza.Identity.IntegrationTests.Me;

/// <summary>T016: a user's own profile and password (L4; IAM-06-AC3).</summary>
[Collection(IdentityTestGroup.Name)]
public sealed class MeApiTests(IdentityApp app) : IAsyncLifetime
{
    private static readonly Uri s_me = new("/api/v1/me", UriKind.Relative);
    private static readonly Uri s_password = new("/api/v1/me/password", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await app.ResetDataAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task L4_a_user_sees_their_own_profile_and_roles()
    {
        var (user, session) = await app.UserWithSessionAsync("ravi.k", SystemRole.AppDesigner);
        using var _ = session;

        var profile = await session.Client.GetFromJsonAsync<JsonElement>(s_me, Ct);

        profile.GetProperty("id").GetGuid().ShouldBe(user.Id);
        profile.GetProperty("username").GetString().ShouldBe("ravi.k");
        profile.GetProperty("email").GetString().ShouldBe("ravi.k@example.org");
        profile.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["AppDesigner", "User"], ignoreOrder: true);
    }

    [Fact]
    public async Task IAM_06_AC3_a_user_changes_their_own_password_and_only_the_new_one_works()
    {
        var (_, session) = await app.UserWithSessionAsync("meena.r");
        using var _ = session;

        var changed = await session.Client.PostAsJsonAsync(s_password, new { currentPassword = IdentityAppUsers.Password, newPassword = "A-Brand-New-Pass-9" }, Ct);
        changed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var browser = app.CreateBrowser();
        var old = await LoginBrowser.PostLoginAsync(browser, "meena.r", IdentityAppUsers.Password, null, Ct);
        LoginBrowser.AlertText(await old.Content.ReadAsStringAsync(Ct)).ShouldBe("Username or password is incorrect.");
        using var withNew = await app.LoginAsync("meena.r", "A-Brand-New-Pass-9");
    }

    [Fact]
    public async Task IAM_06_AC3_the_current_password_must_be_right()
    {
        var (_, session) = await app.UserWithSessionAsync("priya.s");
        using var _ = session;

        var response = await session.Client.PostAsJsonAsync(s_password, new { currentPassword = "not-my-password", newPassword = "A-Brand-New-Pass-9" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").GetProperty("currentPassword")
            .EnumerateArray().Select(e => e.GetString()).ShouldContain("passwords.current_wrong");
    }

    // Review finding 7: guessing the current password on the change-password API counts toward the lockout.
    [Fact]
    public async Task IAM_07_AC1_five_wrong_current_passwords_on_change_password_lock_the_account()
    {
        var (_, session) = await app.UserWithSessionAsync("kiran.l");
        using var _ = session;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var wrong = await session.Client.PostAsJsonAsync(s_password, new { currentPassword = $"guess-{attempt}", newPassword = "A-Brand-New-Pass-9" }, Ct);
            wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var right = await session.Client.PostAsJsonAsync(s_password, new { currentPassword = IdentityAppUsers.Password, newPassword = "A-Brand-New-Pass-9" }, Ct);
        right.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "locked: even the right password is refused now");
        (await right.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString().ShouldBe("passwords.locked_out");

        using var browser = app.CreateBrowser();
        var login = await LoginBrowser.PostLoginAsync(browser, "kiran.l", IdentityAppUsers.Password, null, Ct);
        LoginBrowser.AlertText(await login.Content.ReadAsStringAsync(Ct)).ShouldStartWith("This account is locked");
    }

    [Fact]
    public async Task IAM_06_AC1_the_new_password_follows_the_rules_here_too()
    {
        var (_, session) = await app.UserWithSessionAsync("asha.n");
        using var _ = session;

        var response = await session.Client.PostAsJsonAsync(s_password, new { currentPassword = IdentityAppUsers.Password, newPassword = "asha.n" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var codes = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").GetProperty("newPassword")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        codes.ShouldContain("passwords.too_short");
        codes.ShouldContain("passwords.same_as_username");
    }

    [Fact]
    public async Task IAM_05_AC1_the_Me_API_refuses_anonymous_callers_and_service_tokens()
    {
        using var anonymous = app.CreateClient();
        (await anonymous.GetAsync(s_me, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var serviceToken = (await OidcClient.ClientCredentialsAsync(anonymous, "flowza-runtime", IdentityApp.RuntimeSecret, Ct))
            .GetProperty("access_token").GetString()!;
        (await OidcClient.GetWithTokenAsync(anonymous, "/api/v1/me", serviceToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
