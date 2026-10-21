using Flowza.ServiceDefaults.Security;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Identity.Infrastructure.Persistence;

/// <summary>Identity's own start-up source for the deny list: inactive users from its database (O(inactive users)).</summary>
internal sealed class InactiveUsersSource(IdentityServiceDbContext context) : IUserDenyListSource
{
    public async Task<IReadOnlyCollection<Guid>> InactiveUserIdsAsync(CancellationToken cancellationToken) =>
        await context.Users.AsNoTracking().Where(u => !u.IsActive).Select(u => u.Id).ToListAsync(cancellationToken);
}
