using System.Globalization;
using Microsoft.AspNetCore.Authentication;

namespace Flowza.Gateway.Session;

/// <summary>
/// The browser session the Gateway keeps as BFF (tech discussion T5, SE1; plan 001 §5.4): one encrypted, HTTP-only,
/// SameSite=Strict cookie that holds the user's tokens. The browser never sees a token.
/// </summary>
public static class GatewaySession
{
    public const string CookieScheme = "FlowzaSession";
    public const string OidcScheme = "FlowzaOidc";

    /// <summary>IAM-08 (C1): the session ends after 30 minutes without a request (sliding).</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    /// <summary>IAM-08 (C1): the session ends 12 hours after login, even when the user is active.</summary>
    public static readonly TimeSpan AbsoluteLimit = TimeSpan.FromHours(12);

    /// <summary>The sliding cookie is re-issued at most this often (IAM-08 accuracy vs. one Set-Cookie per request).</summary>
    public static readonly TimeSpan CookieRenewalInterval = TimeSpan.FromMinutes(1);

    /// <summary>The access token is renewed when it has less than this left, so a forwarded token never runs out on the way.</summary>
    public static readonly TimeSpan RefreshBefore = TimeSpan.FromMinutes(2);

    /// <summary>Scopes the Gateway asks for: identity, name and roles, a refresh token, and the Flowza APIs.</summary>
    public static readonly IReadOnlyList<string> Scopes = ["openid", "profile", "roles", "offline_access", "flowza_api"];

    internal const string LoginAtItem = ".flowza.login_at";
    internal const string AccessToken = "access_token";
    internal const string RefreshToken = "refresh_token";
    internal const string IdToken = "id_token";
    internal const string ExpiresAt = "expires_at";

    /// <summary>"__Host-" binds the cookie to the Gateway's own origin; it needs HTTPS, so development (HTTP only) uses a plain name.</summary>
    public static string CookieName(bool isDevelopment) => isDevelopment ? "flowza-session" : "__Host-flowza-session";
}

/// <summary>The time rules of a session (IAM-08); pure functions of the cookie's properties and the clock.</summary>
public static class SessionRules
{
    public static void MarkLogin(AuthenticationProperties properties, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(properties);
        properties.Items[GatewaySession.LoginAtItem] = now.ToString("O", CultureInfo.InvariantCulture);
    }

    /// <summary>False when the login time is missing or 12 hours or more have passed since it.</summary>
    public static bool IsWithinAbsoluteLimit(AuthenticationProperties properties, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return properties.Items.TryGetValue(GatewaySession.LoginAtItem, out var value)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var loginAt)
            && now - loginAt < GatewaySession.AbsoluteLimit;
    }

    /// <summary>True when the access token expires within <see cref="GatewaySession.RefreshBefore"/> (or its expiry is unknown).</summary>
    public static bool AccessTokenNeedsRefresh(AuthenticationProperties properties, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return !TryGetExpiry(properties, out var expiresAt) || expiresAt - now <= GatewaySession.RefreshBefore;
    }

    /// <summary>True when the access token has run out (or its expiry is unknown).</summary>
    public static bool IsAccessTokenExpired(AuthenticationProperties properties, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return !TryGetExpiry(properties, out var expiresAt) || expiresAt <= now;
    }

    /// <summary>IAM-08: renew the sliding cookie when at least a minute has passed since it was last issued.</summary>
    public static bool ShouldRenewCookie(TimeSpan elapsedSinceIssued) => elapsedSinceIssued >= GatewaySession.CookieRenewalInterval;

    /// <summary>Stores renewed tokens the same way the OpenID Connect handler stored the first ones.</summary>
    public static void StoreRefreshedTokens(AuthenticationProperties properties, RefreshedTokens tokens)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(tokens);
        properties.UpdateTokenValue(GatewaySession.AccessToken, tokens.AccessToken);
        properties.UpdateTokenValue(GatewaySession.ExpiresAt, tokens.ExpiresAt.ToString("O", CultureInfo.InvariantCulture));
        if (tokens.RefreshToken is { Length: > 0 } refreshToken)
        {
            properties.UpdateTokenValue(GatewaySession.RefreshToken, refreshToken);
        }

        if (tokens.IdToken is { Length: > 0 } idToken)
        {
            properties.UpdateTokenValue(GatewaySession.IdToken, idToken);
        }
    }

    private static bool TryGetExpiry(AuthenticationProperties properties, out DateTimeOffset expiresAt) =>
        DateTimeOffset.TryParse(properties.GetTokenValue(GatewaySession.ExpiresAt), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out expiresAt);
}
