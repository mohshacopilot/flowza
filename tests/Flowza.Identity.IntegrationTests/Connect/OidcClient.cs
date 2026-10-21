using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Flowza.Identity.IntegrationTests.Infrastructure;

namespace Flowza.Identity.IntegrationTests.Connect;

/// <summary>What the Gateway and the services do against Identity's token server, written out step by step for tests.</summary>
public static class OidcClient
{
    public const string GatewayScopes = "openid profile roles offline_access flowza_api";

    public static async Task<JsonElement> ClientCredentialsAsync(HttpClient client, string clientId, string secret, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var response = await client.PostAsync(
            new Uri("/connect/token", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = secret,
                ["scope"] = "flowza_api",
            }),
            ct);
        return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    public static (string Verifier, string Challenge) NewPkce()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (verifier, Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
    }

    public static Uri AuthorizeUri(string challenge, string scopes = GatewayScopes) => new(
        "/connect/authorize?client_id=flowza-gateway&response_type=code" +
        $"&redirect_uri={Uri.EscapeDataString(IdentityApp.GatewayRedirectUri)}&scope={Uri.EscapeDataString(scopes)}" +
        $"&state=s1&nonce=n1&code_challenge={challenge}&code_challenge_method=S256",
        UriKind.Relative);

    public static async Task<HttpResponseMessage> RedeemCodeAsync(HttpClient client, string code, string verifier, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        return await client.PostAsync(
            new Uri("/connect/token", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = "flowza-gateway",
                ["client_secret"] = IdentityApp.GatewaySecret,
                ["code"] = code,
                ["redirect_uri"] = IdentityApp.GatewayRedirectUri,
                ["code_verifier"] = verifier,
            }),
            ct);
    }

    public static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        return await client.PostAsync(
            new Uri("/connect/token", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = "flowza-gateway",
                ["client_secret"] = IdentityApp.GatewaySecret,
                ["refresh_token"] = refreshToken,
            }),
            ct);
    }

    public static async Task<HttpResponseMessage> GetWithTokenAsync(HttpClient client, string path, string accessToken, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request, ct);
    }

    /// <summary>The claims part of a JWT (no signature check; the service under test does that).</summary>
    public static JsonElement ReadJwtPayload(string jwt)
    {
        ArgumentNullException.ThrowIfNull(jwt);
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        return JsonDocument.Parse(Convert.FromBase64String(payload)).RootElement.Clone();
    }

    public static string? QueryValue(Uri location, string name)
    {
        ArgumentNullException.ThrowIfNull(location);
        var query = (location.IsAbsoluteUri ? location.Query : new Uri(new Uri("http://x"), location).Query).TrimStart('?');
        return query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(pair => pair[0] == name)
            .Select(pair => Uri.UnescapeDataString(pair.Length > 1 ? pair[1] : string.Empty))
            .FirstOrDefault();
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
