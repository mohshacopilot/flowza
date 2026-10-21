using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.IntegrationTests.Connect;

namespace Flowza.Identity.IntegrationTests.Infrastructure;

/// <summary>A logged-in user's API access, as the Gateway would hold it after the code flow.</summary>
public sealed record ApiSession(HttpClient Client, string AccessToken, string RefreshToken) : IDisposable
{
    public void Dispose() => Client.Dispose();
}

public static class ApiSessions
{
    /// <summary>Creates a user with the given roles and logs them in through the real code flow.</summary>
    public static async Task<(User User, ApiSession Session)> UserWithSessionAsync(this IdentityApp app, string username, params SystemRole[] roles)
    {
        var user = await app.CreateUserAsync(username, roles: roles);
        return (user, await app.LoginAsync(username, IdentityAppUsers.Password));
    }

    public static async Task<ApiSession> LoginAsync(this IdentityApp app, string username, string password)
    {
        ArgumentNullException.ThrowIfNull(app);
        var ct = TestContext.Current.CancellationToken;
        using var browser = app.CreateBrowser();
        var (verifier, challenge) = OidcClient.NewPkce();
        var authorize = OidcClient.AuthorizeUri(challenge).ToString();

        var login = await LoginBrowser.PostLoginAsync(browser, username, password, authorize, ct);
        login.StatusCode.ShouldBe(HttpStatusCode.Redirect, $"login of {username} failed");
        var redirect = await browser.GetAsync(new Uri(authorize, UriKind.Relative), ct);
        var code = OidcClient.QueryValue(redirect.Headers.Location!, "code").ShouldNotBeNull();
        using var tokens = await OidcClient.RedeemCodeAsync(browser, code, verifier, ct);
        var body = await tokens.Content.ReadFromJsonAsync<JsonElement>(ct);

        var accessToken = body.GetProperty("access_token").GetString()!;
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return new ApiSession(client, accessToken, body.GetProperty("refresh_token").GetString()!);
    }
}
