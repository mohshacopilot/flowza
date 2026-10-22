using Flowza.Common.Results;
using Flowza.Contracts;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.Application.Abstractions;

// Ports of the Identity application layer (dependency inversion, constitution 4.3): use cases depend on these small
// interfaces; the infrastructure layer implements them with EF Core and ASP.NET Core Identity.

/// <summary>Loads and adds <see cref="User"/> aggregates (tracked, for changes).</summary>
public interface IUserRepository
{
    Task<User?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<bool> IsUsernameTakenAsync(string normalizedUsername, CancellationToken cancellationToken);

    Task<bool> IsEmailTakenAsync(string normalizedEmail, Guid? exceptUserId, CancellationToken cancellationToken);

    /// <summary>Whether the database holds any user at all, inactive ones included (demo data, PLT-07-AC2).</summary>
    Task<bool> AnyUserAsync(CancellationToken cancellationToken);

    /// <summary>Whether any user (active or not) holds Platform Admin, for the first-admin set-up (IAM-03).</summary>
    Task<bool> AnyPlatformAdminAsync(CancellationToken cancellationToken);

    /// <summary>Active users holding Platform Admin, for the last-admin rule (L3).</summary>
    Task<int> CountActivePlatformAdminsAsync(CancellationToken cancellationToken);

    void Add(User user);

    /// <summary>The row version the client last saw; a newer save by someone else makes the save a conflict (constitution 8.5).</summary>
    void SetExpectedVersion(User user, byte[] rowVersion);
}

/// <summary>Passwords, lockout and sessions of a user: everything the domain does not see (plan 001 §5.1).</summary>
public interface IUserCredentials
{
    /// <summary>Checks the password policy, then replaces the password and ends the user's other sessions.</summary>
    Task<Result> SetPasswordAsync(User user, string password, CancellationToken cancellationToken);

    /// <summary>The user's own change: the current password must be right (IAM-06-AC3).</summary>
    Task<Result> ChangePasswordAsync(User user, string currentPassword, string newPassword, CancellationToken cancellationToken);

    Task<bool> IsLockedOutAsync(User user, CancellationToken cancellationToken);

    /// <summary>Ends a lockout early (IAM-07-AC1, K3).</summary>
    Task UnlockAsync(User user, CancellationToken cancellationToken);

    /// <summary>Ends every session and token of the user at once (IAM-04-AC3, K7).</summary>
    Task EndSessionsAsync(User user, CancellationToken cancellationToken);
}

/// <summary>Saves all changes of one use case in one transaction, together with its outgoing events (constitution 3.3).</summary>
public interface IUnitOfWork
{
    /// <summary>
    /// An event that is not a domain event of a changed aggregate (e.g. a login); it is saved with the next
    /// <see cref="SaveChangesAsync"/>. The domain events of changed users are added automatically.
    /// </summary>
    void AddEvent(IntegrationEvent integrationEvent);

    /// <summary>
    /// Expected database failures come back as errors, never as exceptions: a concurrent change is a conflict
    /// (constitution 8.5) and a duplicate username or email a validation error (C3, C4).
    /// </summary>
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Creates one-time temporary passwords (IAM-04-AC2, K6; IAM-03, C2).</summary>
public interface ITemporaryPasswordGenerator
{
    string Generate();
}
