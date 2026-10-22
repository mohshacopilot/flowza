using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Flowza.Gateway.Session;

/// <summary>The logged-in user as the web app needs it: display name and system roles, nothing else.</summary>
public sealed record CurrentUserResponse(string DisplayName, IReadOnlyList<string> Roles);

/// <summary>
/// The web app's BFF endpoints (plan 001 §9; contract in <c>src/Web/src/shared/auth</c>): login and logout are
/// full-page navigations so the Gateway can run the OpenID Connect redirects; <c>/bff/user</c> answers 401 without a session.
/// </summary>
public static class BffEndpoints
{
    public static IEndpointRouteBuilder MapBffEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var bff = app.MapGroup("/bff").ExcludeFromDescription();
        bff.MapGet("/login", Login).AllowAnonymous();
        bff.MapGet("/logout", Logout).AllowAnonymous();
        bff.MapGet("/user", CurrentUser).RequireAuthorization();
        return app;
    }

    private static IResult Login(string? returnUrl) => Results.Challenge(
        new AuthenticationProperties { RedirectUri = LocalUrl.OrRoot(returnUrl) },
        [GatewaySession.OidcScheme]);

    /// <summary>Ends the Gateway session and Identity's login, then returns to the start page.</summary>
    private static IResult Logout(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        ? Results.SignOut(new AuthenticationProperties { RedirectUri = "/" }, [GatewaySession.CookieScheme, GatewaySession.OidcScheme])
        : Results.Redirect("/");

    private static IResult CurrentUser(ClaimsPrincipal user, HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new CurrentUserResponse(
            user.FindFirst("name")?.Value ?? string.Empty,
            [.. user.FindAll("role").Select(claim => claim.Value)]));
    }
}

/// <summary>Only paths on the Gateway itself are accepted as a return address, so a link cannot send users to another site after login.</summary>
public static class LocalUrl
{
    public static string OrRoot(string? url) => IsLocal(url) ? url! : "/";

    public static bool IsLocal(string? url) =>
        url is { Length: > 0 }
        && url[0] == '/'
        && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'))
        && !url.Any(char.IsControl);
}
