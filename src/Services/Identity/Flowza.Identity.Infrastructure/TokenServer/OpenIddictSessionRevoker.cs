using Flowza.Identity.Infrastructure.AspNetIdentity;
using OpenIddict.Abstractions;

namespace Flowza.Identity.Infrastructure.TokenServer;

/// <summary>
/// Ends every token and authorization of a user (K7): refresh fails at once; access tokens are refused by Identity
/// at once and by other services through the deny list (plan 001 §5.4, PA4).
/// </summary>
internal sealed class OpenIddictSessionRevoker(
    IOpenIddictTokenManager tokens,
    IOpenIddictAuthorizationManager authorizations) : ISessionRevoker
{
    public async Task RevokeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var subject = userId.ToString();
        await authorizations.RevokeBySubjectAsync(subject, cancellationToken);
        await tokens.RevokeBySubjectAsync(subject, cancellationToken);
    }
}
