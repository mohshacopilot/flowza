using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.Application.Users.SetUserRoles;

/// <summary>Sets a user's system roles (IAM-02, L2); User is always kept, the last active Platform Admin keeps the role (L3).</summary>
public sealed record SetUserRolesCommand(Guid Id, IReadOnlyList<string>? Roles);

public sealed class SetUserRolesHandler(IUserRepository users, IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(SetUserRolesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var roles = RoleNames.Parse(command.Roles);
        if (roles.IsFailure)
        {
            return roles.Error;
        }

        var user = await users.GetAsync(command.Id, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        var changed = user.SetRoles(roles.Value, await users.CountActivePlatformAdminsAsync(cancellationToken));
        return changed.IsFailure ? changed : await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
