using Microsoft.AspNetCore.Authentication;
using Yarp.ReverseProxy.Transforms;

namespace Flowza.Gateway.Session;

/// <summary>
/// IAM-04-AC3 (owner decision 2026-10-22, amends PA4): a service refuses a session's token at once when the user was
/// deactivated (Identity revokes the tokens; every service keeps its event-fed deny list, T018). The Gateway then ends
/// the session too, so the browser is logged out with that same answer and cannot log in again.
/// </summary>
public static partial class SessionEnding
{
    private static readonly object s_identityUnavailableKey = new();

    /// <summary>The refresh failed because Identity could not be reached, and the forwarded token has run out.</summary>
    internal static void MarkIdentityUnavailable(HttpContext context) => context.Items[s_identityUnavailableKey] = true;

    /// <summary>
    /// End the session only when the service says the <b>token</b> is no good (review finding 5): OpenIddict validation
    /// answers a revoked, deny-listed or invalid token with <c>WWW-Authenticate: Bearer error="invalid_token"</c>; any other
    /// 401 leaves the session alone. And never while Identity is down (review finding 4): the user did nothing wrong.
    /// </summary>
    public static bool ShouldEnd(HttpContext context, HttpResponseMessage? serviceResponse)
    {
        ArgumentNullException.ThrowIfNull(context);
        return serviceResponse is not null
            && context.User.Identity?.IsAuthenticated == true
            && !context.Items.ContainsKey(s_identityUnavailableKey)
            && IsRefusedToken(serviceResponse);
    }

    public static bool IsRefusedToken(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            && response.Headers.WwwAuthenticate.Any(challenge =>
                string.Equals(challenge.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
                && challenge.Parameter?.Contains("error=\"invalid_token\"", StringComparison.Ordinal) == true);
    }

    /// <summary>YARP response transform on the API routes.</summary>
    internal static async ValueTask EndWhenTokenRefusedAsync(ResponseTransformContext context)
    {
        if (!ShouldEnd(context.HttpContext, context.ProxyResponse))
        {
            return;
        }

        await context.HttpContext.SignOutAsync(GatewaySession.CookieScheme);
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<SessionLogCategory>>();
        var userId = context.HttpContext.User.FindFirst("sub")?.Value;
        LogEnded(logger, userId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Session of user {UserId} ended: a service refused its access token")]
    private static partial void LogEnded(ILogger logger, string? userId);
}

/// <summary>Log category of the session's end (static classes cannot be one).</summary>
internal sealed class SessionLogCategory;
