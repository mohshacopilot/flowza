using Flowza.Common.Ids;
using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.Application.Users.CreateUser;

/// <summary>An admin creates a user (IAM-04-AC1; no self sign-up). The answer carries a temporary password once.</summary>
public sealed record CreateUserCommand(string? Username, string? DisplayName, string? Email, string? Mobile, IReadOnlyList<string>? Roles);

/// <summary>The temporary password is in this answer only; it is stored as a hash and never logged (K6).</summary>
public sealed record CreatedUser(Guid Id, string TemporaryPassword);

public sealed class CreateUserHandler(
    IUserRepository users,
    IUserCredentials credentials,
    ITemporaryPasswordGenerator passwords,
    IUnitOfWork unitOfWork,
    SequentialGuidGenerator ids)
{
    public async Task<Result<CreatedUser>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var roles = RoleNames.Parse(command.Roles);
        if (roles.IsFailure)
        {
            return roles.Error;
        }

        var created = User.Create(ids.NewGuid(), command.Username, command.DisplayName, command.Email, command.Mobile, roles.Value);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var user = created.Value;
        if (await users.IsUsernameTakenAsync(user.NormalizedUsername, cancellationToken))
        {
            return UserErrors.UsernameTaken;
        }

        if (await users.IsEmailTakenAsync(user.NormalizedEmail, exceptUserId: null, cancellationToken))
        {
            return UserErrors.EmailTaken;
        }

        users.Add(user);
        var temporaryPassword = passwords.Generate();
        var password = await credentials.SetPasswordAsync(user, temporaryPassword, cancellationToken);
        if (password.IsFailure)
        {
            return password.Error;
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? new CreatedUser(user.Id, temporaryPassword) : saved.Error;
    }
}
