using Flowza.Common.Security;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace Flowza.ServiceDefaults.Security;

/// <summary>
/// The logged-in user of the current HTTP request (task T036): audit columns and event actors record the real user.
/// Outside a request, or for a service's own token, it is the system (<c>null</c>).
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId =>
        httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
        && Guid.TryParse(user.GetClaim(OpenIddictConstants.Claims.Subject) ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : null;
}
