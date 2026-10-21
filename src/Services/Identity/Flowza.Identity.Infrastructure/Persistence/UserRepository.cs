using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Users;
using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Identity.Infrastructure.Persistence;

internal sealed class UserRepository(IdentityServiceDbContext context) : IUserRepository
{
    public Task<User?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(username);
        var normalized = Username.Normalize(username);
        return context.Users.FirstOrDefaultAsync(u => u.NormalizedUsername == normalized, cancellationToken);
    }

    public Task<bool> IsUsernameTakenAsync(string normalizedUsername, CancellationToken cancellationToken) =>
        context.Users.AnyAsync(u => u.NormalizedUsername == normalizedUsername, cancellationToken);

    public Task<bool> IsEmailTakenAsync(string normalizedEmail, Guid? exceptUserId, CancellationToken cancellationToken) =>
        context.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail && u.Id != exceptUserId, cancellationToken);

    public Task<int> CountActivePlatformAdminsAsync(CancellationToken cancellationToken)
    {
        var adminRoleId = SystemRole.PlatformAdmin.Id;
        return context.Users.CountAsync(
            u => u.IsActive && u.RoleAssignments.Any(r => r.RoleId == adminRoleId),
            cancellationToken);
    }

    public void Add(User user)
    {
        context.Users.Add(user);

        // Every user can be locked out by wrong passwords (K3). Set explicitly: EF would store the CLR default (false).
        context.Entry(user).Property(UserCredentialColumns.LockoutEnabled).CurrentValue = true;
    }

    public void SetExpectedVersion(User user, byte[] rowVersion) => context.SetExpectedRowVersion(user, rowVersion);
}
