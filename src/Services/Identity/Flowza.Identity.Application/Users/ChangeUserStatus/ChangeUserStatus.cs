using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.Application.Users.ChangeUserStatus;

/// <summary>Deactivation ends the user's sessions at once (IAM-04-AC3, K7); the last active Platform Admin is protected (L3).</summary>
public sealed record DeactivateUserCommand(Guid Id);

/// <summary>Re-activation is allowed; history stays unchanged (IAM-04-AC5, C6).</summary>
public sealed record ActivateUserCommand(Guid Id);

public sealed class DeactivateUserHandler(IUserRepository users, IUserCredentials credentials, IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(DeactivateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var user = await users.GetAsync(command.Id, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (!user.IsActive)
        {
            return Result.Success();
        }

        var deactivated = user.Deactivate(await users.CountActivePlatformAdminsAsync(cancellationToken));
        if (deactivated.IsFailure)
        {
            return deactivated;
        }

        await credentials.EndSessionsAsync(user, cancellationToken);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ActivateUserHandler(IUserRepository users, IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(ActivateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var user = await users.GetAsync(command.Id, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        user.Activate();
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
