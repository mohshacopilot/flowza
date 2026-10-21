using System.Collections.Immutable;
using System.Security.Claims;
using Flowza.Identity.Domain.Users;
using Flowza.ServiceDefaults.Security;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Flowza.Identity.Api.Connect;

/// <summary>
/// Builds what goes into tokens (plan 001 §5.4). Access tokens carry IDs and roles only; the display name and
/// username go only into the identity token, which the Gateway keeps in its encrypted cookie (T5, SE1).
/// </summary>
internal static class TokenPrincipal
{
    private const string AuthenticationType = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme;

    public static ClaimsPrincipal ForUser(User user, ImmutableArray<string> scopes)
    {
        var identity = new ClaimsIdentity(AuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Name, user.DisplayName)
            .SetClaim(Claims.PreferredUsername, user.Username.Value)
            .SetClaims(Claims.Role, [.. user.Roles.Select(r => r.Name)]);

        identity.SetScopes(scopes);
        identity.SetResources(ApiSecurityExtensions.Audience);
        identity.SetDestinations(claim => DestinationsFor(claim, scopes));
        return new ClaimsPrincipal(identity);
    }

    /// <summary>A service calling another service (client credentials, SE3): the subject is the client.</summary>
    public static ClaimsPrincipal ForClient(string clientId, ImmutableArray<string> scopes)
    {
        var identity = new ClaimsIdentity(AuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, clientId).SetClaim(Claims.ClientId, clientId);
        identity.SetScopes(scopes);
        identity.SetResources(ApiSecurityExtensions.Audience);
        identity.SetDestinations(_ => [Destinations.AccessToken]);
        return new ClaimsPrincipal(identity);
    }

    private static IEnumerable<string> DestinationsFor(Claim claim, ImmutableArray<string> scopes) => claim.Type switch
    {
        Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
        Claims.Name or Claims.PreferredUsername when scopes.Contains(Scopes.Profile) => [Destinations.IdentityToken],
        Claims.Role when scopes.Contains(Scopes.Roles) => [Destinations.AccessToken, Destinations.IdentityToken],
        Claims.Role => [Destinations.AccessToken],
        _ => [],
    };
}
