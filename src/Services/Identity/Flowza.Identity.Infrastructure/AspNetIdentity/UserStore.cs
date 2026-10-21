using Flowza.Identity.Domain.Users;
using Flowza.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Identity.Infrastructure.AspNetIdentity;

/// <summary>
/// Lets ASP.NET Core Identity (password hashing, lockout, security stamp) work on the domain <see cref="User"/>
/// (adapter). Credentials are shadow columns (<see cref="UserCredentialColumns"/>). Only the store interfaces
/// Flowza needs are implemented. The store never saves: every use case saves once through the unit of work, so
/// credential changes and their events share one transaction (constitution 3.3).
/// </summary>
internal sealed class UserStore(IdentityServiceDbContext context) :
    IUserPasswordStore<User>,
    IUserSecurityStampStore<User>,
    IUserLockoutStore<User>
{
    public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Id.ToString());

    public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.Username.Value);

    public Task SetUserNameAsync(User user, string? userName, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Usernames never change (C4).");

    public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.NormalizedUsername);

    // The domain keeps the normalized name in step with the name; nothing to do.
    public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        context.Users.Add(user);
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        if (context.Entry(user).State == EntityState.Detached)
        {
            throw new InvalidOperationException("Credentials can only be changed on a user loaded for change (tracked).");
        }

        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Users are deactivated, never deleted (constitution 8.6).");

    public Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        Guid.TryParse(userId, out var id)
            ? context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            : Task.FromResult<User?>(null);

    public Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        context.Users.FirstOrDefaultAsync(u => u.NormalizedUsername == normalizedUserName, cancellationToken);

    public Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken cancellationToken)
    {
        Set(user, UserCredentialColumns.PasswordHash, passwordHash);
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(Get<string?>(user, UserCredentialColumns.PasswordHash));

    public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(Get<string?>(user, UserCredentialColumns.PasswordHash) is not null);

    public Task SetSecurityStampAsync(User user, string stamp, CancellationToken cancellationToken)
    {
        Set(user, UserCredentialColumns.SecurityStamp, stamp);
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(Get<string?>(user, UserCredentialColumns.SecurityStamp));

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(Get<DateTimeOffset?>(user, UserCredentialColumns.LockoutEnd));

    public Task SetLockoutEndDateAsync(User user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        Set(user, UserCredentialColumns.LockoutEnd, lockoutEnd);
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        var count = Get<int>(user, UserCredentialColumns.AccessFailedCount) + 1;
        Set(user, UserCredentialColumns.AccessFailedCount, count);
        return Task.FromResult(count);
    }

    public Task ResetAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        Set(user, UserCredentialColumns.AccessFailedCount, 0);
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(Get<int>(user, UserCredentialColumns.AccessFailedCount));

    public Task<bool> GetLockoutEnabledAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(Get<bool>(user, UserCredentialColumns.LockoutEnabled));

    public Task SetLockoutEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
    {
        Set(user, UserCredentialColumns.LockoutEnabled, enabled);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // The DbContext belongs to the request scope.
    }

    private T Get<T>(User user, string column) => (T)context.Entry(user).Property(column).CurrentValue!;

    private void Set<T>(User user, string column, T value) => context.Entry(user).Property(column).CurrentValue = value;
}
