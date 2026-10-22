using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Flowza.Gateway.Session;

/// <summary>
/// Checks the session cookie on every request: the 12-hour limit (IAM-08) and a fresh access token. Requests
/// without a valid session get 401/403, never a redirect: the web app decides when to go to the login (/bff/login).
/// </summary>
internal sealed partial class SessionCookieEvents(TimeProvider time, ITokenRefresher refresher, ILogger<SessionCookieEvents> logger) : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    /// <summary>
    /// IAM-08: 30 minutes without a request, measured from the last request. ASP.NET Core renews a sliding cookie only
    /// after half its lifetime, which would end a session after about 17 idle minutes; renewing at most once a minute
    /// keeps the error under a minute (phase 2 review, finding 1).
    /// </summary>
    public override Task CheckSlidingExpiration(CookieSlidingExpirationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (SessionRules.ShouldRenewCookie(context.ElapsedTime))
        {
            context.ShouldRenew = true;
        }

        return Task.CompletedTask;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var now = time.GetUtcNow();
        if (!SessionRules.IsWithinAbsoluteLimit(context.Properties, now))
        {
            await EndSessionAsync(context);
            return;
        }

        if (!SessionRules.AccessTokenNeedsRefresh(context.Properties, now))
        {
            return;
        }

        var refreshToken = context.Properties.GetTokenValue(GatewaySession.RefreshToken);
        TokenRefreshOutcome outcome;
        try
        {
            outcome = refreshToken is null
                ? TokenRefreshOutcome.Rejected
                : await refresher.RefreshAsync(refreshToken, context.HttpContext.RequestAborted);
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            // The browser gave up; the refresh itself goes on and its answer waits for the next request (single flight).
            return;
        }

        switch (outcome.Status)
        {
            case TokenRefreshStatus.Refreshed:
                SessionRules.StoreRefreshedTokens(context.Properties, outcome.Tokens!);
                context.ShouldRenew = true;
                break;
            case TokenRefreshStatus.Rejected:
                LogRejected(logger, context.Principal?.FindFirst("sub")?.Value);
                await EndSessionAsync(context);
                break;
            case TokenRefreshStatus.Unavailable:
            default:
                // Keep the session; the request goes on with the old token. If that token has run out, the service's
                // 401 must not end the session either: Identity is down, the user did nothing wrong (review finding 4).
                if (SessionRules.IsAccessTokenExpired(context.Properties, now))
                {
                    SessionEnding.MarkIdentityUnavailable(context.HttpContext);
                }

                break;
        }
    }

    private static async Task EndSessionAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(GatewaySession.CookieScheme);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Session of user {UserId} ended: Identity refused its refresh token")]
    private static partial void LogRejected(ILogger logger, string? userId);
}
