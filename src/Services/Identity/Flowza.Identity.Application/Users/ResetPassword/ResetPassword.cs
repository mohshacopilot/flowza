using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.Application.Users.ResetPassword;

/// <summary>
/// An admin resets a password (K5, K6): a temporary password shown once; the user must change it at the next login
/// (IAM-04-AC2). The old password and every session of the user stop working.
/// </summary>
public sealed record ResetPasswordCommand(Guid Id);

public sealed record TemporaryPassword(string Password);

public sealed class ResetPasswordHandler(
    IUserRepository users,
    IUserCredentials credentials,
    ITemporaryPasswordGenerator passwords,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<TemporaryPassword>> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var user = await users.GetAsync(command.Id, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        var temporary = passwords.Generate();
        var set = await credentials.SetPasswordAsync(user, temporary, cancellationToken);
        if (set.IsFailure)
        {
            return set.Error;
        }

        user.PasswordReset();
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? new TemporaryPassword(temporary) : saved.Error;
    }
}

/// <summary>Ends a lockout early (IAM-07-AC1, K3).</summary>
public sealed record UnlockUserCommand(Guid Id);

public sealed class UnlockUserHandler(IUserRepository users, IUserCredentials credentials, IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(UnlockUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var user = await users.GetAsync(command.Id, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        await credentials.UnlockAsync(user, cancellationToken);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
