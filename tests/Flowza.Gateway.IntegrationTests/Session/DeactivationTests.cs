using System.Diagnostics;
using System.Net;
using Flowza.Gateway.IntegrationTests.Infrastructure;
using Flowza.Gateway.Protection;
using Flowza.Identity.Domain.Users;

namespace Flowza.Gateway.IntegrationTests.Session;

/// <summary>Deactivation ends the user's session at once (IAM-04-AC3, K7; owner decision 2026-10-22 on PA4).</summary>
[Collection(GatewayTestGroup.Name)]
public sealed class DeactivationTests(GatewayPlatform platform)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task IAM_04_AC3_deactivated_user_is_refused_within_1_second_and_logged_out()
    {
        platform.Clock.Now = DateTimeOffset.UtcNow;
        var admin = await platform.CreateUserAsync(SystemRole.PlatformAdmin);
        var user = await platform.CreateUserAsync();
        using var adminBrowser = platform.CreateBrowser();
        using var userBrowser = platform.CreateBrowser();
        using (await Browser.LogInAsync(adminBrowser, admin, "/", Ct))
        using (await Browser.LogInAsync(userBrowser, user, "/", Ct))
        {
        }

        (await userBrowser.GetAsync(new Uri("/api/identity/v1/me", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var deactivate = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/identity/v1/users/{user.Id}/deactivate", UriKind.Relative));
        deactivate.Headers.Add(AntiForgeryHeaderMiddleware.HeaderName, AntiForgeryHeaderMiddleware.HeaderValue);
        (await adminBrowser.SendAsync(deactivate, Ct)).IsSuccessStatusCode.ShouldBeTrue("the admin deactivates the user through the Gateway");
        var deactivatedAt = Stopwatch.StartNew();

        // The user's very next call is refused ...
        using var refused = await userBrowser.GetAsync(new Uri("/api/identity/v1/me", UriKind.Relative), Ct);
        refused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        deactivatedAt.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));

        // ... the Gateway ended the session with it ...
        (await userBrowser.GetAsync(new Uri("/bff/user", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // ... and logging in again fails with the general message (IAM-01-AC2): the login page comes back.
        using var again = await Browser.LogInAsync(userBrowser, user, "/", Ct);
        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        again.RequestMessage!.RequestUri!.AbsolutePath.ShouldBe("/account/login");

        // The admin's own session is not affected.
        (await adminBrowser.GetAsync(new Uri("/bff/user", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
