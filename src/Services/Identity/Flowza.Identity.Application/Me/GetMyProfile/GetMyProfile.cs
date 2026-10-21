using Flowza.Common.Results;
using Flowza.Identity.Application.Users;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.Application.Me.GetMyProfile;

/// <summary>Every user sees their own profile and roles (L4; plan 001 §5.3 <c>GET /api/v1/me</c>).</summary>
public sealed record GetMyProfileQuery(Guid UserId);

public sealed record MyProfile(Guid Id, string Username, string DisplayName, string Email, string? Mobile, IReadOnlyList<string> Roles);

public sealed class GetMyProfileHandler(IUserReadStore users)
{
    public async Task<Result<MyProfile>> Handle(GetMyProfileQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var user = await users.GetAsync(query.UserId, cancellationToken);
        return user is null || user.Status == UserStatus.Inactive
            ? UserErrors.NotFound
            : new MyProfile(user.Id, user.Username, user.DisplayName, user.Email, user.Mobile, user.Roles);
    }
}
