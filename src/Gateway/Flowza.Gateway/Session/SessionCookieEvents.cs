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
        var outcome = refreshToken is null
            ? TokenRefreshOutcome.Rejected
            : await refresher.RefreshAsync(refreshToken, context.HttpContext.RequestAborted);

        switch (outcome.Status)
        {
            case TokenRefreshStatus.Refreshed:
                SessionRules.StoreRefreshedTokens(context.Properties, outcome.Tokens!, now);
                context.ShouldRenew = true;
                break;
            case TokenRefreshStatus.Rejected:
                LogRejected(logger, context.Principal?.FindFirst("sub")?.Value);
                await EndSessionAsync(context);
                break;
            case TokenRefreshStatus.Unavailable:
            default:
                // Keep the session: the service refuses the old token once it has run out, and the next request retries.
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
