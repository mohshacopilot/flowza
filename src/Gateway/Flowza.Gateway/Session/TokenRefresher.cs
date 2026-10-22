using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Flowza.Gateway.Session;

/// <summary>Tokens from a refresh; <paramref name="ExpiresAt"/> is when the access token runs out (Gateway clock).</summary>
public sealed record RefreshedTokens(string AccessToken, string? RefreshToken, string? IdToken, DateTimeOffset ExpiresAt);

public enum TokenRefreshStatus
{
    Refreshed,

    /// <summary>Identity refused the refresh token (user deactivated, token revoked or expired): the session ends.</summary>
    Rejected,

    /// <summary>Identity could not be reached or failed: the session stays, the request goes on with the old token.</summary>
    Unavailable,
}

public sealed record TokenRefreshOutcome(TokenRefreshStatus Status, RefreshedTokens? Tokens = null)
{
    public static readonly TokenRefreshOutcome Rejected = new(TokenRefreshStatus.Rejected);
    public static readonly TokenRefreshOutcome Unavailable = new(TokenRefreshStatus.Unavailable);
}

/// <summary>Renews a session's access token with its refresh token (plan 001 §9 "token refresh before expiry").</summary>
public interface ITokenRefresher
{
    Task<TokenRefreshOutcome> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}

/// <summary>
/// Calls Identity's token endpoint directly (back channel), with the Gateway's client credentials and the same HTTP
/// client and discovery document as the OpenID Connect handler.
/// </summary>
internal sealed partial class OidcTokenRefresher(
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
    TimeProvider time,
    ILogger<OidcTokenRefresher> logger) : ITokenRefresher
{
    /// <summary>A refresh that takes longer counts as "Identity unavailable".</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public async Task<TokenRefreshOutcome> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var options = oidcOptions.Get(GatewaySession.OidcScheme);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            var configuration = await options.ConfigurationManager!.GetConfigurationAsync(timeout.Token);
            using var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = options.ClientId!,
                    ["client_secret"] = options.ClientSecret!,
                }),
            };
            using var response = await options.Backchannel.SendAsync(request, timeout.Token);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                return TokenRefreshOutcome.Rejected;
            }

            if (!response.IsSuccessStatusCode)
            {
                LogUnavailable(logger, (int)response.StatusCode);
                return TokenRefreshOutcome.Unavailable;
            }

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(timeout.Token);
            return body is { AccessToken.Length: > 0, ExpiresIn: > 0 }
                ? new TokenRefreshOutcome(
                    TokenRefreshStatus.Refreshed,
                    new RefreshedTokens(body.AccessToken, body.RefreshToken, body.IdToken, time.GetUtcNow().AddSeconds(body.ExpiresIn.Value)))
                : TokenRefreshOutcome.Unavailable;
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Identity down, too slow or its discovery document unreadable: the request fails cleanly further on.
            LogUnreachable(logger, exception.GetType().Name);
            return TokenRefreshOutcome.Unavailable;
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("id_token")] string? IdToken,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token refresh failed: Identity answered {StatusCode}")]
    private static partial void LogUnavailable(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token refresh failed: Identity unreachable ({ErrorType})")]
    private static partial void LogUnreachable(ILogger logger, string errorType);
}
