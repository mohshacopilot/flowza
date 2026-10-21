using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.Application.Users.UpdateUser;

/// <summary>
/// An admin edits display name, email and mobile (IAM-04-AC1). The username is not part of the command: it never
/// changes (C4). <see cref="Version"/> is the version the admin saw; a newer save by someone else is a conflict (8.5).
/// </summary>
public sealed record UpdateUserCommand(Guid Id, string? DisplayName, string? Email, string? Mobile, string? Version);

public sealed class UpdateUserHandler(IUserRepository users, IUnitOfWork unitOfWork)
{
    public static Error VersionRequired { get; } = Error.Validation(
        "common.version_required",
        "The version of the record you edited is missing.",
        new Dictionary<string, string[]> { ["version"] = ["common.version_required"] });

    public async Task<Result> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var version = UserVersion.Decode(command.Version);
        if (version is null)
        {
            return VersionRequired;
        }

        var user = await users.GetAsync(command.Id, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        // Checked before the user is changed, so a refused update leaves the aggregate as it was.
        if (Email.Create(command.Email) is { IsSuccess: true } email
            && await users.IsEmailTakenAsync(email.Value.Normalized, user.Id, cancellationToken))
        {
            return UserErrors.EmailTaken;
        }

        var updated = user.UpdateProfile(command.DisplayName, command.Email, command.Mobile);
        if (updated.IsFailure)
        {
            return updated;
        }

        users.SetExpectedVersion(user, version);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
