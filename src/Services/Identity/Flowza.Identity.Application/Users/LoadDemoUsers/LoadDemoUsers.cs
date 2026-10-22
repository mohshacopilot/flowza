using Flowza.Common.Ids;
using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.Application.Users.LoadDemoUsers;

/// <summary>One demo user; the names and addresses are seed data and live in the demo tool, not in the platform.</summary>
public sealed record DemoUser(string Username, string DisplayName, string Email, IReadOnlyList<string> Roles);

/// <summary>
/// Loads the demo users into an <b>empty</b> identity database (PLT-07, C5): all of them share the documented demo
/// password and need not change it. Refused, with nothing changed, when the database has any user (PLT-07-AC2).
/// </summary>
public sealed record LoadDemoUsersCommand(IReadOnlyList<DemoUser> Users, string Password);

/// <summary>Whether the identity database already holds business data (any user), so the demo tool must refuse.</summary>
public sealed record IdentityHasDataQuery;

public sealed class LoadDemoUsersHandler(
    IUserRepository users,
    IUserCredentials credentials,
    IUnitOfWork unitOfWork,
    SequentialGuidGenerator ids,
    TimeProvider timeProvider)
{
    public static Error NotEmpty { get; } = Error.Conflict(
        "demo.database_not_empty",
        "The identity database already has users. Demo data is only loaded into empty databases; nothing was changed.");

    public async Task<Result<int>> Handle(LoadDemoUsersCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Checked again here, inside the use case, in case data arrived after the tool's first check.
        if (await users.AnyUserAsync(cancellationToken))
        {
            return NotEmpty;
        }

        foreach (var demo in command.Users)
        {
            var roles = RoleNames.Parse(demo.Roles);
            if (roles.IsFailure)
            {
                return roles.Error;
            }

            var created = User.Create(ids.NewGuid(), demo.Username, demo.DisplayName, demo.Email, mobile: null, roles.Value);
            if (created.IsFailure)
            {
                return created.Error;
            }

            var user = created.Value;
            users.Add(user);
            var password = await credentials.SetPasswordAsync(user, command.Password, cancellationToken);
            if (password.IsFailure)
            {
                return password.Error;
            }

            // C5: demo users keep the documented demo password; there is no forced change at the first login.
            user.PasswordChanged(timeProvider.GetUtcNow());
        }

        // All users in one transaction: either every demo user is there, or none.
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? command.Users.Count : saved.Error;
    }
}

public sealed class IdentityHasDataHandler(IUserRepository users)
{
    public Task<bool> Handle(IdentityHasDataQuery query, CancellationToken cancellationToken) => users.AnyUserAsync(cancellationToken);
}
