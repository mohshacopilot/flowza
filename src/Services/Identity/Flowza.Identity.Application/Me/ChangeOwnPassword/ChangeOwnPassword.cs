using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.Domain.Users.Passwords;

namespace Flowza.Identity.Application.Me.ChangeOwnPassword;

/// <summary>A logged-in user changes their own password after entering the current one (IAM-06-AC3, L4; IAM-03, K6).</summary>
public sealed record ChangeOwnPasswordCommand(Guid UserId, string? CurrentPassword, string? NewPassword);

public sealed class ChangeOwnPasswordHandler(
    IUserRepository users,
    IUserCredentials credentials,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result> Handle(ChangeOwnPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrEmpty(command.CurrentPassword))
        {
            return PasswordErrors.CurrentPasswordWrong;
        }

        var user = await users.GetAsync(command.UserId, cancellationToken);
        if (user is not { IsActive: true })
        {
            return UserErrors.NotFound;
        }

        var changed = await credentials.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword ?? string.Empty, cancellationToken);
        if (changed.IsFailure)
        {
            return changed;
        }

        user.PasswordChanged(timeProvider.GetUtcNow());
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
