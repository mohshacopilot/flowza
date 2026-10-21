using Flowza.Common.Ids;
using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.Application.Users.InitializeAdmin;

/// <summary>
/// The first Platform Admin of a new installation (IAM-03, C2; plan 001 §5.4, PA5). Refused when any Platform Admin
/// already exists, so it can never be used to add a second way in.
/// </summary>
public sealed record InitializeAdminCommand(string? Username, string? DisplayName, string? Email);

/// <summary>The generated password is shown once, by the caller, to the installer's console only (C2).</summary>
public sealed record InitializedAdmin(Guid Id, string Username, string TemporaryPassword);

public sealed class InitializeAdminHandler(
    IUserRepository users,
    IUserCredentials credentials,
    ITemporaryPasswordGenerator passwords,
    IUnitOfWork unitOfWork,
    SequentialGuidGenerator ids)
{
    public static Error AdminExists { get; } = Error.Conflict(
        "users.platform_admin_exists",
        "A Platform Admin already exists. Nothing was changed; use the users screen instead.");

    public async Task<Result<InitializedAdmin>> Handle(InitializeAdminCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await users.AnyPlatformAdminAsync(cancellationToken))
        {
            return AdminExists;
        }

        var created = User.Create(ids.NewGuid(), command.Username, command.DisplayName, command.Email, mobile: null, [SystemRole.PlatformAdmin]);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var admin = created.Value;
        if (await users.IsUsernameTakenAsync(admin.NormalizedUsername, cancellationToken))
        {
            return UserErrors.UsernameTaken;
        }

        if (await users.IsEmailTakenAsync(admin.NormalizedEmail, exceptUserId: null, cancellationToken))
        {
            return UserErrors.EmailTaken;
        }

        users.Add(admin);
        var temporaryPassword = passwords.Generate();
        var password = await credentials.SetPasswordAsync(admin, temporaryPassword, cancellationToken);
        if (password.IsFailure)
        {
            return password.Error;
        }

        // A new user must change the password at first login (User.Create sets MustChangePassword; IAM-03-AC1).
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? new InitializedAdmin(admin.Id, admin.Username.Value, temporaryPassword) : saved.Error;
    }
}
