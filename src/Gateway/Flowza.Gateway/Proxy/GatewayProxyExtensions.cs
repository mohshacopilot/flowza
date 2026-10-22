using System.Net;
using System.Net.Http.Headers;
using Flowza.Gateway.Protection;
using Flowza.Gateway.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;

namespace Flowza.Gateway.Proxy;

/// <summary>
/// Routes (GW1) from the <c>ReverseProxy</c> configuration section: <c>/api/{service}/…</c> to the services,
/// <c>/connect/**</c> and <c>/account/**</c> to Identity, in development everything else to the web app's Vite server.
/// Destinations are found by Aspire service discovery in development and set in configuration elsewhere (GW6).
/// </summary>
public static class GatewayProxyExtensions
{
    public const string Section = "ReverseProxy";

    /// <summary>Route metadata: "true" on API routes, which get the session's access token instead of the browser's cookies.</summary>
    public const string ForwardAccessTokenKey = "Flowza.ForwardAccessToken";

    public static TBuilder AddGatewayProxy<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection(Section))
            .AddServiceDiscoveryDestinationResolver()
            .AddTransforms(context =>
            {
                var forwardToken = context.Route.Metadata?.TryGetValue(ForwardAccessTokenKey, out var value) == true
                    && bool.TryParse(value, out var enabled) && enabled;
                context.AddRequestTransform(IdentityHeaders.RemoveAsync);
                context.AddRequestTransform(forwardToken ? ForwardAccessTokenAsync : RemoveSessionCookie);
                if (forwardToken)
                {
                    context.AddResponseTransform(EndSessionWhenTokenRefusedAsync);
                }
            });
        return builder;
    }

    /// <summary>
    /// SE1: services see a bearer token, never the browser's cookies. Anything the browser sent as Authorization is
    /// dropped, so a caller cannot pass its own token through the Gateway.
    /// </summary>
    private static async ValueTask ForwardAccessTokenAsync(RequestTransformContext context)
    {
        context.ProxyRequest.Headers.Remove(HeaderNames.Cookie);
        context.ProxyRequest.Headers.Authorization = null;
        var token = await context.HttpContext.GetTokenAsync(GatewaySession.CookieScheme, GatewaySession.AccessToken);
        if (token is not null)
        {
            context.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    /// <summary>
    /// IAM-04-AC3 (owner decision 2026-10-22, amends PA4): a service refuses a session's token at once when the user
    /// was deactivated (Identity revokes the tokens; every service keeps its event-fed deny list, T018). The Gateway
    /// then ends the session too, so the browser is logged out with that same answer and cannot log in again.
    /// </summary>
    private static async ValueTask EndSessionWhenTokenRefusedAsync(ResponseTransformContext context)
    {
        if (context.ProxyResponse?.StatusCode == HttpStatusCode.Unauthorized
            && context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            await context.HttpContext.SignOutAsync(GatewaySession.CookieScheme);
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(GatewayProxyExtensions));
            var userId = context.HttpContext.User.FindFirst("sub")?.Value;
            SessionLog.EndedByService(logger, userId);
        }
    }

    /// <summary>Identity's pages and the web app keep their own cookies, but never receive the Gateway's session cookie.</summary>
    private static ValueTask RemoveSessionCookie(RequestTransformContext context)
    {
        if (context.ProxyRequest.Headers.TryGetValues(HeaderNames.Cookie, out var values))
        {
            var cookieName = context.HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(GatewaySession.CookieScheme).Cookie.Name!;
            var remaining = CookieHeader.Without(string.Join("; ", values), cookieName);
            context.ProxyRequest.Headers.Remove(HeaderNames.Cookie);
            if (remaining.Length > 0)
            {
                context.ProxyRequest.Headers.TryAddWithoutValidation(HeaderNames.Cookie, remaining);
            }
        }

        return ValueTask.CompletedTask;
    }
}

internal static partial class SessionLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Session of user {UserId} ended: a service refused its access token")]
    public static partial void EndedByService(ILogger logger, string? userId);
}

/// <summary>Removes one cookie, including the chunks ASP.NET Core splits a large cookie into (<c>nameC1</c>, <c>nameC2</c>, …).</summary>
public static class CookieHeader
{
    public static string Without(string cookieHeader, string cookieName)
    {
        ArgumentNullException.ThrowIfNull(cookieHeader);
        ArgumentException.ThrowIfNullOrEmpty(cookieName);
        var kept = cookieHeader
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(pair => !IsSessionCookie(pair.Split('=', 2)[0], cookieName));
        return string.Join("; ", kept);
    }

    private static bool IsSessionCookie(string name, string cookieName) =>
        string.Equals(name, cookieName, StringComparison.Ordinal)
        || (name.StartsWith(cookieName + "C", StringComparison.Ordinal)
            && name.Length > cookieName.Length + 1
            && name[(cookieName.Length + 1)..].All(char.IsAsciiDigit));
}
