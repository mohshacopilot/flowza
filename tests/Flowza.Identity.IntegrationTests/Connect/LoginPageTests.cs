using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Resources;
using System.Text.Json;
using System.Text.RegularExpressions;
using Flowza.Identity.Api;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.Infrastructure;
using Flowza.Identity.IntegrationTests.Infrastructure;

namespace Flowza.Identity.IntegrationTests.Connect;

/// <summary>T014: Identity's login and change-password pages, and the complete code flow through them (SE1, SE2).</summary>
[Collection(IdentityTestGroup.Name)]
public sealed partial class LoginPageTests(IdentityApp app) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await app.ResetDataAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task IAM_01_AC2_wrong_password_and_unknown_username_show_the_same_general_message()
    {
        await app.CreateUserAsync("ravi.k");
        using var browser = app.CreateBrowser();

        var wrongPassword = await LoginBrowser.PostLoginAsync(browser, "ravi.k", "Wrong-Password-1", null, Ct);
        var unknownUser = await LoginBrowser.PostLoginAsync(browser, "nobody.here", "Wrong-Password-1", null, Ct);

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.OK);
        unknownUser.StatusCode.ShouldBe(HttpStatusCode.OK);
        var first = LoginBrowser.AlertText(await wrongPassword.Content.ReadAsStringAsync(Ct));
        var second = LoginBrowser.AlertText(await unknownUser.Content.ReadAsStringAsync(Ct));
        first.ShouldBe("Username or password is incorrect.");
        second.ShouldBe(first);
    }

    [Fact]
    public async Task IAM_07_AC1_a_locked_account_is_told_that_it_is_locked()
    {
        await app.CreateUserAsync("priya.s");
        using var browser = app.CreateBrowser();

        HttpResponseMessage response = null!;
        for (var attempt = 0; attempt < IdentityInfrastructureExtensions.LockoutAttempts; attempt++)
        {
            response = await LoginBrowser.PostLoginAsync(browser, "priya.s", "Wrong-Password-1", null, Ct);
        }

        LoginBrowser.AlertText(await response.Content.ReadAsStringAsync(Ct))!.ShouldContain("locked");
    }

    [Fact]
    public async Task IAM_01_a_user_logs_in_through_the_code_flow_and_the_tokens_carry_id_and_roles_only()
    {
        var user = await app.CreateUserAsync("asha.n", roles: SystemRole.AppDesigner);
        using var browser = app.CreateBrowser();
        var (verifier, challenge) = OidcClient.NewPkce();
        var authorize = OidcClient.AuthorizeUri(challenge).ToString();

        var login = await LoginBrowser.PostLoginAsync(browser, "asha.n", IdentityAppUsers.Password, authorize, Ct);
        login.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        login.Headers.Location!.ToString().ShouldBe(authorize);

        var code = await AuthorizeForCodeAsync(browser, authorize);
        using var tokens = await OidcClient.RedeemCodeAsync(browser, code, verifier, Ct);
        tokens.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await tokens.Content.ReadFromJsonAsync<JsonElement>(Ct);

        var access = OidcClient.ReadJwtPayload(body.GetProperty("access_token").GetString()!);
        access.GetProperty("sub").GetString().ShouldBe(user.Id.ToString());
        access.GetProperty("role").EnumerateArray().Select(r => r.GetString()).ShouldBe(["AppDesigner", "User"], ignoreOrder: true);
        access.TryGetProperty("name", out _).ShouldBeFalse("access tokens carry IDs and roles, not names");
        access.TryGetProperty("email", out _).ShouldBeFalse();

        var identity = OidcClient.ReadJwtPayload(body.GetProperty("id_token").GetString()!);
        identity.GetProperty("name").GetString().ShouldBe("Test asha.n");

        (await OidcClient.GetWithTokenAsync(browser, "/api/v1/info", body.GetProperty("access_token").GetString()!, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        using var refreshed = await OidcClient.RefreshAsync(browser, body.GetProperty("refresh_token").GetString()!, Ct);
        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IAM_03_and_IAM_04_AC2_a_temporary_password_must_be_changed_before_the_code_flow_continues()
    {
        await app.CreateUserAsync("new.admin", password: "Temporary-Pass-1", mustChangePassword: true, SystemRole.PlatformAdmin);
        using var browser = app.CreateBrowser();
        var (verifier, challenge) = OidcClient.NewPkce();
        var authorize = OidcClient.AuthorizeUri(challenge).ToString();

        var login = await LoginBrowser.PostLoginAsync(browser, "new.admin", "Temporary-Pass-1", authorize, Ct);
        login.Headers.Location!.ToString().ShouldStartWith("/account/change-password");

        // Going straight to the code flow does not help: it sends the user back to the change page.
        var skipped = await browser.GetAsync(new Uri(authorize, UriKind.Relative), Ct);
        skipped.Headers.Location!.ToString().ShouldStartWith("/account/change-password");

        var changePage = login.Headers.Location.ToString();
        var weak = await LoginBrowser.PostFormAsync(browser, changePage, ChangeFields("Temporary-Pass-1", "1234567890", authorize), Ct);
        LoginBrowser.AlertText(await weak.Content.ReadAsStringAsync(Ct))!.ShouldContain("too common");

        var changed = await LoginBrowser.PostFormAsync(browser, changePage, ChangeFields("Temporary-Pass-1", "My-Own-Strong-Pass-7", authorize), Ct);
        changed.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        changed.Headers.Location!.ToString().ShouldBe(authorize);

        var code = await AuthorizeForCodeAsync(browser, authorize);
        (await OidcClient.RedeemCodeAsync(browser, code, verifier, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The temporary password no longer works; the new one does.
        using var other = app.CreateBrowser();
        LoginBrowser.AlertText(await (await LoginBrowser.PostLoginAsync(other, "new.admin", "Temporary-Pass-1", null, Ct)).Content.ReadAsStringAsync(Ct))
            .ShouldBe("Username or password is incorrect.");
        (await LoginBrowser.PostLoginAsync(other, "new.admin", "My-Own-Strong-Pass-7", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task The_login_page_never_redirects_to_another_site()
    {
        await app.CreateUserAsync("ravi.k");
        using var browser = app.CreateBrowser();

        var response = await LoginBrowser.PostLoginAsync(browser, "ravi.k", IdentityAppUsers.Password, "https://evil.test/", Ct);

        response.Headers.Location!.ToString().ShouldBe("/");
    }

    [Theory]
    [InlineData("/account/login")]
    public async Task Constitution_12_2_every_field_has_a_label_and_the_page_declares_its_language(string page)
    {
        using var browser = app.CreateBrowser();

        var html = await browser.GetStringAsync(new Uri(page, UriKind.Relative), Ct);

        html.ShouldContain("<html lang=\"en\">");
        var inputIds = InputId().Matches(html).Select(m => m.Groups[1].Value).ToList();
        inputIds.ShouldNotBeEmpty();
        foreach (var id in inputIds)
        {
            html.ShouldContain($"<label for=\"{id}\">");
        }
    }

    [Fact]
    public void Constitution_12_1_every_text_key_of_the_pages_is_in_the_resource_file()
    {
        var resources = new ResourceManager("Flowza.Identity.Api.Resources.AccountTexts", typeof(AccountTexts).Assembly);

        foreach (var key in AccountTexts.AllKeys.Concat(AccountTexts.ErrorCodes))
        {
            resources.GetString(key, CultureInfo.InvariantCulture).ShouldNotBeNullOrWhiteSpace($"missing text '{key}'");
        }
    }

    private static Dictionary<string, string> ChangeFields(string current, string newPassword, string returnUrl) => new()
    {
        ["CurrentPassword"] = current,
        ["NewPassword"] = newPassword,
        ["ConfirmPassword"] = newPassword,
        ["ReturnUrl"] = returnUrl,
    };

    private static async Task<string> AuthorizeForCodeAsync(HttpClient browser, string authorize)
    {
        var response = await browser.GetAsync(new Uri(authorize, UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!;
        location.ToString().ShouldStartWith(IdentityApp.GatewayRedirectUri);
        return OidcClient.QueryValue(location, "code").ShouldNotBeNull();
    }

    [GeneratedRegex("<input id=\"([^\"]+)\"")]
    private static partial Regex InputId();
}
