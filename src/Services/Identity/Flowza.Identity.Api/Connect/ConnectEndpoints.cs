using System.Security.Claims;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Users;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Flowza.Identity.Api.Connect;

/// <summary>
/// The OpenID Connect endpoints whose decisions are Flowza's (plan 001 §5.3–5.4): who may get a code or token, and
/// what goes into it. OpenIddict checks the protocol (client, redirect address, PKCE, scopes) before these run.
/// </summary>
public static class ConnectEndpoints
{
    public static IEndpointRouteBuilder MapConnectEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], AuthorizeAsync).AllowAnonymous().ExcludeFromDescription();
        app.MapPost("/connect/token", TokenAsync).AllowAnonymous().ExcludeFromDescription();
        app.MapMethods("/connect/logout", [HttpMethods.Get, HttpMethods.Post], (Delegate)LogoutAsync).AllowAnonymous().ExcludeFromDescription();
        return app;
    }

    /// <summary>Code flow (SE1): the user logs in on Identity's own page, then gets a code for the Gateway.</summary>
    private static async Task<IResult> AuthorizeAsync(HttpContext context, IUserRepository users, CancellationToken cancellationToken)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be read.");

        var login = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var user = login.Succeeded ? await FindActiveUserAsync(users, login.Principal, cancellationToken) : null;
        if (user is null)
        {
            // Not logged in (or deactivated meanwhile): to the login page, then back here.
            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = context.Request.PathBase + context.Request.Path + context.Request.QueryString },
                [IdentityConstants.ApplicationScheme]);
        }

        if (user.MustChangePassword)
        {
            // IAM-03 / IAM-04-AC2: nothing else before the new password.
            var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            return Results.Redirect($"/account/change-password?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        return Results.SignIn(TokenPrincipal.ForUser(user, request.GetScopes()), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> TokenAsync(HttpContext context, IUserRepository users, CancellationToken cancellationToken)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be read.");

        if (request.IsClientCredentialsGrantType())
        {
            // OpenIddict has already checked the client's secret and its permission for the requested scopes (SE3).
            return Results.SignIn(TokenPrincipal.ForClient(request.ClientId!, request.GetScopes()), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var stored = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var user = stored.Succeeded ? await FindActiveUserAsync(users, stored.Principal, cancellationToken) : null;
            if (user is null)
            {
                // Deactivated, or never existed: no new tokens (IAM-04-AC3).
                return Forbid(Errors.InvalidGrant, "The user can no longer log in.");
            }

            // Fresh claims, so a role change takes effect at the next refresh; scopes stay those first granted.
            return Results.SignIn(TokenPrincipal.ForUser(user, stored.Principal!.GetScopes()), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Forbid(Errors.UnsupportedGrantType, "This grant type is not supported.");
    }

    private static async Task<IResult> LogoutAsync(HttpContext context)
    {
        await context.SignOutAsync(IdentityConstants.ApplicationScheme);

        // OpenIddict sends the browser back to the client's registered post-logout address.
        return Results.SignOut(new AuthenticationProperties { RedirectUri = "/" }, [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static async Task<User?> FindActiveUserAsync(IUserRepository users, ClaimsPrincipal? principal, CancellationToken cancellationToken)
    {
        var subject = principal?.FindFirst(Claims.Subject)?.Value ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return null;
        }

        var user = await users.GetAsync(userId, cancellationToken);
        return user is { IsActive: true } ? user : null;
    }

    private static IResult Forbid(string error, string description) => Results.Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        }),
        [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
