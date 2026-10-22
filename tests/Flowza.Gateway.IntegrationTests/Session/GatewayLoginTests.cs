using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Flowza.Gateway.IntegrationTests.Infrastructure;
using Flowza.Gateway.Session;
using Flowza.Identity.Domain.Users;

namespace Flowza.Gateway.IntegrationTests.Session;

/// <summary>Login, API calls and logout through the Gateway as BFF (T020; IAM-01, IAM-08, SE1).</summary>
[Collection(GatewayTestGroup.Name)]
public sealed class GatewayLoginTests(GatewayPlatform platform)
{
    private const string Home = "/home";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task IAM_01_AC1_user_logs_in_through_the_gateway_and_after_logout_pages_and_apis_are_refused()
    {
        platform.Clock.Now = DateTimeOffset.UtcNow;
        var user = await platform.CreateUserAsync(SystemRole.AppDesigner);
        using var browser = platform.CreateBrowser();

        using var landed = await Browser.LogInAsync(browser, user, Home, Ct);
        landed.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        landed.Headers.Location!.OriginalString.ShouldBe(Home, "back to the page the user started from");

        var me = await browser.GetFromJsonAsync<CurrentUserResponse>(new Uri("/bff/user", UriKind.Relative), Ct);
        me!.DisplayName.ShouldBe(user.DisplayName);
        me.Roles.ShouldBe([SystemRole.User.Name, SystemRole.AppDesigner.Name], ignoreOrder: true);

        // The Gateway forwards the session's access token: Identity's protected endpoint answers.
        using (var api = await browser.GetAsync(new Uri("/api/identity/v1/info", UriKind.Relative), Ct))
        {
            api.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var loggedOut = await Browser.FollowAsync(browser, await browser.GetAsync(new Uri("/bff/logout", UriKind.Relative), Ct), "/", Ct);
        loggedOut.Headers.Location!.ToString().ShouldEndWith("/", Case.Sensitive, "logout ends on the start page");

        (await browser.GetAsync(new Uri("/bff/user", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await browser.GetAsync(new Uri("/api/identity/v1/info", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Identity's login ended too: logging in again shows the login page instead of signing in silently.
        using var again = await Browser.FollowAsync(browser, await browser.GetAsync(new Uri("/bff/login?returnUrl=%2Fhome", UriKind.Relative), Ct), Home, Ct);
        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        again.RequestMessage!.RequestUri!.AbsolutePath.ShouldBe("/account/login");
    }

    [Fact]
    public async Task IAM_05_AC1_anonymous_calls_through_the_gateway_are_refused()
    {
        using var browser = platform.CreateBrowser();

        (await browser.GetAsync(new Uri("/bff/user", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await browser.GetAsync(new Uri("/api/identity/v1/info", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await browser.GetAsync(new Uri("/api/runtime/v1/info", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SE1_browser_holds_only_an_http_only_same_site_strict_cookie_and_cannot_bring_its_own_token()
    {
        platform.Clock.Now = DateTimeOffset.UtcNow;
        var user = await platform.CreateUserAsync();
        using var browser = platform.CreateBrowser();

        using var landed = await Browser.LogInAsync(browser, user, Home, Ct);
        // The tokens make the cookie large, so ASP.NET Core splits it into chunks; every chunk has the same flags.
        var sessionCookies = landed.Headers.GetValues("Set-Cookie")
            .Where(c => c.StartsWith(GatewaySession.CookieName(isDevelopment: true), StringComparison.Ordinal))
            .ToList();
        sessionCookies.ShouldNotBeEmpty();
        sessionCookies.ShouldAllBe(c => c.Contains("httponly", StringComparison.OrdinalIgnoreCase)
            && c.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase)
            && !c.Contains("expires=", StringComparison.OrdinalIgnoreCase));

        var userBody = await browser.GetStringAsync(new Uri("/bff/user", UriKind.Relative), Ct);
        userBody.ShouldNotContain("token", Case.Insensitive);

        // A token sent by an anonymous caller is not passed on.
        using var stranger = platform.CreateBrowser();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/identity/v1/info", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "made-up");
        (await stranger.SendAsync(request, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SE1_access_token_is_refreshed_before_it_expires()
    {
        platform.Clock.Now = DateTimeOffset.UtcNow;
        var user = await platform.CreateUserAsync();
        using var browser = platform.CreateBrowser();
        using var _ = await Browser.LogInAsync(browser, user, Home, Ct);
        var afterLogin = platform.TokenCalls.Count;

        (await browser.GetAsync(new Uri("/api/identity/v1/info", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstToken = platform.Forwarded.Last("/api/v1/info").Headers["Authorization"];

        // Access tokens live 15 minutes; with 1 minute left the Gateway renews it before forwarding.
        platform.Clock.Now += TimeSpan.FromMinutes(14);
        using var api = await browser.GetAsync(new Uri("/api/identity/v1/info", UriKind.Relative), Ct);

        api.StatusCode.ShouldBe(HttpStatusCode.OK);
        platform.TokenCalls.Count.ShouldBe(afterLogin + 1);
        platform.Forwarded.Last("/api/v1/info").Headers["Authorization"].ShouldNotBe(firstToken, "the new token is forwarded");
        api.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith(GatewaySession.CookieName(isDevelopment: true), StringComparison.Ordinal), "the session keeps the new tokens");
    }

    [Fact]
    public async Task SE1_parallel_requests_share_one_refresh_and_the_session_survives()
    {
        platform.Clock.Now = DateTimeOffset.UtcNow;
        var user = await platform.CreateUserAsync();
        using var browser = platform.CreateBrowser();
        using var _ = await Browser.LogInAsync(browser, user, Home, Ct);
        var afterLogin = platform.TokenCalls.Count;

        // A page load after 14 minutes: several calls at once, all with the same (old) refresh token.
        platform.Clock.Now += TimeSpan.FromMinutes(14);
        var calls = Enumerable.Range(0, 6).Select(_ => browser.GetAsync(new Uri("/api/identity/v1/info", UriKind.Relative), Ct));
        var answers = await Task.WhenAll(calls);

        answers.ShouldAllBe(a => a.StatusCode == HttpStatusCode.OK);
        platform.TokenCalls.Count.ShouldBe(afterLogin + 1, "one refresh for the whole page load");

        // Much later the session still renews normally (no replayed refresh token, no revoked login).
        platform.Clock.Now += TimeSpan.FromMinutes(14);
        (await browser.GetAsync(new Uri("/api/identity/v1/info", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        platform.TokenCalls.Count.ShouldBe(afterLogin + 2);
        foreach (var answer in answers)
        {
            answer.Dispose();
        }
    }

    [Fact]
    public async Task SE1_session_stays_while_identity_cannot_be_reached()
    {
        platform.Clock.Now = DateTimeOffset.UtcNow;
        var user = await platform.CreateUserAsync();
        using var browser = platform.CreateBrowser();
        using var _ = await Browser.LogInAsync(browser, user, Home, Ct);

        platform.TokenCalls.FailWith = HttpStatusCode.ServiceUnavailable;
        try
        {
            platform.Clock.Now += TimeSpan.FromMinutes(16);
            (await browser.GetAsync(new Uri("/bff/user", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK, "refresh failed, session kept");
        }
        finally
        {
            platform.TokenCalls.FailWith = null;
        }

        (await browser.GetAsync(new Uri("/api/identity/v1/info", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK, "Identity is back: refreshed and forwarded");
    }

    [Fact]
    public async Task IAM_08_AC1_session_ends_after_30_minutes_without_a_request()
    {
        platform.Clock.Now = DateTimeOffset.UtcNow;
        var user = await platform.CreateUserAsync();
        using var browser = platform.CreateBrowser();
        using var _ = await Browser.LogInAsync(browser, user, Home, Ct);

        platform.Clock.Now += TimeSpan.FromMinutes(29);
        (await browser.GetAsync(new Uri("/bff/user", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        platform.Clock.Now += TimeSpan.FromMinutes(31);
        (await browser.GetAsync(new Uri("/bff/user", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task IAM_08_AC1_idle_time_counts_from_the_last_request_also_early_in_the_cookie_life()
    {
        platform.Clock.Now = DateTimeOffset.UtcNow;
        var user = await platform.CreateUserAsync();
        using var browser = platform.CreateBrowser();
        using var _ = await Browser.LogInAsync(browser, user, Home, Ct);

        // A request 12 minutes after login, then 29 idle minutes: still logged in (41 minutes after login).
        platform.Clock.Now += TimeSpan.FromMinutes(12);
        (await browser.GetAsync(new Uri("/bff/user", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        platform.Clock.Now += TimeSpan.FromMinutes(29);
        (await browser.GetAsync(new Uri("/bff/user", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        platform.Clock.Now += TimeSpan.FromMinutes(31);
        (await browser.GetAsync(new Uri("/bff/user", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task IAM_01_AC1_another_site_cannot_log_the_user_out_through_the_remote_sign_out_address()
    {
        platform.Clock.Now = DateTimeOffset.UtcNow;
        var user = await platform.CreateUserAsync();
        using var browser = platform.CreateBrowser();
        using var _ = await Browser.LogInAsync(browser, user, Home, Ct);

        using var remote = await browser.GetAsync(new Uri("/signout-oidc", UriKind.Relative), Ct);

        remote.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        remote.Headers.TryGetValues("Set-Cookie", out var cookies).ShouldBeFalse(string.Join(" | ", cookies ?? []));
        (await browser.GetAsync(new Uri("/bff/user", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IAM_08_AC1_active_session_ends_12_hours_after_login()
    {
        platform.Clock.Now = DateTimeOffset.UtcNow;
        var user = await platform.CreateUserAsync();
        using var browser = platform.CreateBrowser();
        using var _ = await Browser.LogInAsync(browser, user, Home, Ct);
        var loginAt = platform.Clock.Now;

        // A request every 20 minutes keeps the session alive (and renews the access token each time) ...
        while (platform.Clock.Now + TimeSpan.FromMinutes(20) < loginAt + GatewaySession.AbsoluteLimit)
        {
            platform.Clock.Now += TimeSpan.FromMinutes(20);
            (await browser.GetAsync(new Uri("/bff/user", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // ... until 12 hours after login.
        platform.Clock.Now = loginAt + GatewaySession.AbsoluteLimit;
        (await browser.GetAsync(new Uri("/bff/user", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
