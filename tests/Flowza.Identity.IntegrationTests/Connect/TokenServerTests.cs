using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flowza.Identity.IntegrationTests.Infrastructure;

namespace Flowza.Identity.IntegrationTests.Connect;

/// <summary>T013: Identity's OpenID Connect server (tech discussion T5, SE1 and SE3).</summary>
[Collection(IdentityTestGroup.Name)]
public sealed class TokenServerTests(IdentityApp app)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SE3_a_service_gets_a_token_with_client_credentials_and_an_API_accepts_it()
    {
        using var client = app.CreateClient();

        var token = await OidcClient.ClientCredentialsAsync(client, "flowza-runtime", IdentityApp.RuntimeSecret, Ct);
        var accessToken = token.GetProperty("access_token").GetString()!;

        token.GetProperty("token_type").GetString().ShouldBe("Bearer");
        token.GetProperty("expires_in").GetInt32().ShouldBeLessThanOrEqualTo(15 * 60);
        var claims = OidcClient.ReadJwtPayload(accessToken);
        claims.GetProperty("sub").GetString().ShouldBe("flowza-runtime");
        claims.GetProperty("aud").ToString().ShouldContain("flowza");

        (await OidcClient.GetWithTokenAsync(client, "/api/v1/info", accessToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SE3_a_wrong_client_secret_gets_no_token()
    {
        using var client = app.CreateClient();

        var token = await OidcClient.ClientCredentialsAsync(client, "flowza-runtime", "not-the-secret", Ct);

        token.GetProperty("error").GetString().ShouldBe("invalid_client");
        token.TryGetProperty("access_token", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task IAM_05_AC1_an_API_refuses_a_forged_token()
    {
        using var client = app.CreateClient();
        var token = (await OidcClient.ClientCredentialsAsync(client, "flowza-runtime", IdentityApp.RuntimeSecret, Ct))
            .GetProperty("access_token").GetString()!;
        var parts = token.Split('.');
        var forged = $"{parts[0]}.{parts[1]}.{new string('A', parts[2].Length)}";

        (await OidcClient.GetWithTokenAsync(client, "/api/v1/info", forged, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SE1_the_discovery_document_offers_code_flow_with_PKCE_client_credentials_and_refresh()
    {
        using var client = app.CreateClient();

        var discovery = await client.GetFromJsonAsync<JsonElement>(new Uri("/.well-known/openid-configuration", UriKind.Relative), Ct);

        discovery.GetProperty("authorization_endpoint").GetString().ShouldEndWith("/connect/authorize");
        discovery.GetProperty("token_endpoint").GetString().ShouldEndWith("/connect/token");
        discovery.GetProperty("end_session_endpoint").GetString().ShouldEndWith("/connect/logout");
        var grants = discovery.GetProperty("grant_types_supported").EnumerateArray().Select(g => g.GetString()).ToList();
        grants.ShouldBe(["authorization_code", "client_credentials", "refresh_token"], ignoreOrder: true);
        discovery.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(m => m.GetString()).ShouldContain("S256");
        discovery.GetProperty("jwks_uri").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task SE1_the_code_flow_sends_a_visitor_who_is_not_logged_in_to_the_login_page()
    {
        using var browser = app.CreateBrowser();
        var (_, challenge) = OidcClient.NewPkce();

        var response = await browser.GetAsync(OidcClient.AuthorizeUri(challenge), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldContain("/account/login");
        Uri.UnescapeDataString(response.Headers.Location.ToString()).ShouldContain("/connect/authorize");
    }

    [Fact]
    public async Task SE1_the_code_flow_requires_PKCE()
    {
        using var browser = app.CreateBrowser();
        var withoutPkce = new Uri(
            "/connect/authorize?client_id=flowza-gateway&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(IdentityApp.GatewayRedirectUri)}&scope=openid&state=s1",
            UriKind.Relative);

        var response = await browser.GetAsync(withoutPkce, Ct);

        // Refused before any login: no code can be issued without a PKCE challenge.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Headers.Location.ShouldBeNull();
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("invalid_request");
    }

    [Fact]
    public async Task SE1_an_unknown_redirect_address_is_refused()
    {
        using var browser = app.CreateBrowser();
        var (_, challenge) = OidcClient.NewPkce();
        var uri = OidcClient.AuthorizeUri(challenge).ToString().Replace(
            Uri.EscapeDataString(IdentityApp.GatewayRedirectUri), Uri.EscapeDataString("https://evil.test/callback"), StringComparison.Ordinal);

        var response = await browser.GetAsync(new Uri(uri, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
