using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Flowza.Gateway.IntegrationTests.Infrastructure;
using Flowza.Gateway.Protection;
using Flowza.Gateway.Session;

namespace Flowza.Gateway.IntegrationTests.Protection;

/// <summary>The Gateway's protection (T021; SE5, GW4, SE7, IAM-07-AC2, GW3, GW5), with Identity behind it.</summary>
[Collection(GatewayTestGroup.Name)]
public sealed class GatewayProtectionTests(GatewayPlatform platform)
{
    private const string ChangePassword = "/api/identity/v1/me/password";
    private const string ChangePasswordAtIdentity = "/api/v1/me/password";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<HttpClient> LoggedInBrowserAsync()
    {
        platform.Clock.Now = DateTimeOffset.UtcNow;
        var user = await platform.CreateUserAsync();
        var browser = platform.CreateBrowser();
        using var _ = await Browser.LogInAsync(browser, user, "/", Ct);
        return browser;
    }

    private static HttpRequestMessage Post(string path, HttpContent content, bool antiForgeryHeader)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = content };
        if (antiForgeryHeader)
        {
            request.Headers.Add(AntiForgeryHeaderMiddleware.HeaderName, AntiForgeryHeaderMiddleware.HeaderValue);
        }

        return request;
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task SE5_change_request_without_the_anti_forgery_header_is_refused()
    {
        using var browser = await LoggedInBrowserAsync();
        var forwardedBefore = platform.Forwarded.Requests.Count(r => r.Path == ChangePasswordAtIdentity);

        using var refused = await browser.SendAsync(Post(ChangePassword, Json("{}"), antiForgeryHeader: false), Ct);
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync(Ct)).ShouldContain(AntiForgeryHeaderMiddleware.ErrorCode);
        platform.Forwarded.Requests.Count(r => r.Path == ChangePasswordAtIdentity).ShouldBe(forwardedBefore, "nothing reached the service");

        using var accepted = await browser.SendAsync(Post(ChangePassword, Json("{}"), antiForgeryHeader: true), Ct);
        accepted.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
        platform.Forwarded.Requests.Count(r => r.Path == ChangePasswordAtIdentity).ShouldBe(forwardedBefore + 1);
    }

    [Fact]
    public async Task GW4_identity_headers_from_the_browser_never_reach_a_service()
    {
        using var browser = await LoggedInBrowserAsync();

        using var api = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/identity/v1/info", UriKind.Relative));
        api.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "made-up");
        api.Headers.Add("X-User-Id", "someone-else");
        api.Headers.Add("Remote-User", "admin");
        api.Headers.Add("X-Flowza-Actor", "admin");
        (await browser.SendAsync(api, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var atService = platform.Forwarded.Last("/api/v1/info").Headers;
        atService.Keys.ShouldNotContain("X-User-Id");
        atService.Keys.ShouldNotContain("Remote-User");
        atService.Keys.ShouldNotContain("X-Flowza-Actor");
        atService.Keys.ShouldNotContain("Cookie", "SE1: services get a token, never the browser's cookies");
        atService["Authorization"].ShouldStartWith("Bearer ");
        atService["Authorization"].ShouldNotContain("made-up", Case.Sensitive, "the session's own token replaces the browser's");

        using var page = new HttpRequestMessage(HttpMethod.Get, new Uri("/account/login", UriKind.Relative));
        page.Headers.Add("X-User-Id", "someone-else");
        (await browser.SendAsync(page, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var atIdentityPage = platform.Forwarded.Last("/account/login").Headers;
        atIdentityPage.Keys.ShouldNotContain("X-User-Id");
        atIdentityPage.GetValueOrDefault("Cookie", string.Empty).ShouldNotContain(GatewaySession.CookieName(isDevelopment: true));
    }

    [Fact]
    public async Task SE7_every_answer_carries_the_security_headers_also_for_identity_pages()
    {
        using var browser = platform.CreateBrowser();

        foreach (var path in new[] { "/bff/user", "/account/login", "/health/live" })
        {
            using var response = await browser.GetAsync(new Uri(path, UriKind.Relative), Ct);
            var headers = response.Headers;
            headers.GetValues("Content-Security-Policy").Single().ShouldBe(SecurityHeaders.DevelopmentContentSecurityPolicy, path);
            headers.GetValues("Content-Security-Policy").Single().ShouldContain("frame-ancestors 'none'");
            headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff", path);
            headers.GetValues("X-Frame-Options").Single().ShouldBe("DENY", $"{path}: replaces Identity's own SAMEORIGIN");
            headers.GetValues("Referrer-Policy").Single().ShouldBe("no-referrer", path);
            headers.Contains("Strict-Transport-Security").ShouldBeFalse("HSTS only over HTTPS, outside Development");
        }
    }

    [Fact]
    public async Task IAM_07_AC2_many_login_attempts_from_one_address_are_refused()
    {
        using var guesser = platform.CreateBrowser("10.99.0.1");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["Username"] = "nobody", ["Password"] = "guess" });

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            using var response = await guesser.PostAsync(new Uri("/account/login", UriKind.Relative), form, Ct);
            response.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests, $"attempt {attempt}");
        }

        using var refused = await guesser.PostAsync(new Uri("/account/login", UriKind.Relative), form, Ct);
        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        refused.Headers.Contains("Retry-After").ShouldBeTrue();
        (await refused.Content.ReadAsStringAsync(Ct)).ShouldContain(GatewayProtectionExtensions.RateLimitedCode);

        // Opening the page is not an attempt, and another address is not affected.
        (await guesser.GetAsync(new Uri("/account/login", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var other = platform.CreateBrowser("10.99.0.2");
        (await other.PostAsync(new Uri("/account/login", UriKind.Relative), form, Ct)).StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task GW3_api_calls_are_limited_per_user()
    {
        using var busy = await LoggedInBrowserAsync();
        using var calm = await LoggedInBrowserAsync();

        for (var call = 1; call <= GatewayPlatform.ApiPermitLimit; call++)
        {
            using var response = await busy.GetAsync(new Uri("/bff/user", UriKind.Relative), Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"call {call}");
        }

        (await busy.GetAsync(new Uri("/api/identity/v1/info", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await calm.GetAsync(new Uri("/api/identity/v1/info", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK, "another user has their own limit");
    }

    [Fact]
    public async Task GW5_request_body_over_1_MB_is_refused()
    {
        using var browser = await LoggedInBrowserAsync();
        var forwardedBefore = platform.Forwarded.Requests.Count(r => r.Path == ChangePasswordAtIdentity);

        using var tooLarge = await browser.SendAsync(Post(ChangePassword, new ByteArrayContent(new byte[(1024 * 1024) + 1]), antiForgeryHeader: true), Ct);
        tooLarge.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await tooLarge.Content.ReadAsStringAsync(Ct)).ShouldContain(RequestBodyLimitMiddleware.ErrorCode);
        platform.Forwarded.Requests.Count(r => r.Path == ChangePasswordAtIdentity).ShouldBe(forwardedBefore);

        using var atLimit = await browser.SendAsync(Post(ChangePassword, new ByteArrayContent(new byte[1024 * 1024]), antiForgeryHeader: true), Ct);
        atLimit.StatusCode.ShouldNotBe(HttpStatusCode.RequestEntityTooLarge);
    }
}
