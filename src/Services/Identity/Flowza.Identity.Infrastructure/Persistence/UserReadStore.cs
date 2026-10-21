using Flowza.Common.Paging;
using Flowza.Identity.Application.Users;
using Flowza.Identity.Domain.Users;
using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Identity.Infrastructure.Persistence;

/// <summary>
/// No-tracking projections for the user screens (CQRS-lite, T3 D4). The list is ordered by username and paged with
/// an indexed sort (unique index on the normalized username): one index range per page, plus a count.
/// </summary>
internal sealed class UserReadStore(IdentityServiceDbContext context, TimeProvider timeProvider) : IUserReadStore
{
    public async Task<PagedResult<UserListItem>> ListAsync(UserListFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(page);
        var now = timeProvider.GetUtcNow();
        var users = context.Users.AsNoTracking();

        if (filter.Search is { } search)
        {
            // Case-insensitive collation (D5); EF sends the text as a parameter (constitution 7.4).
            var normalized = search.ToUpperInvariant();
            users = users.Where(u => u.NormalizedUsername.Contains(normalized)
                || u.DisplayName.Contains(search)
                || u.NormalizedEmail.Contains(normalized));
        }

        users = filter.Status switch
        {
            UserStatus.Inactive => users.Where(u => !u.IsActive),
            UserStatus.Locked => users.Where(u => u.IsActive && EF.Property<DateTimeOffset?>(u, UserCredentialColumns.LockoutEnd) > now),
            UserStatus.Active => users.Where(u => u.IsActive
                && (EF.Property<DateTimeOffset?>(u, UserCredentialColumns.LockoutEnd) == null
                    || EF.Property<DateTimeOffset?>(u, UserCredentialColumns.LockoutEnd) <= now)),
            _ => users,
        };

        var total = await users.CountAsync(cancellationToken);
        var rows = await users
            .OrderBy(u => u.NormalizedUsername)
            .Skip(page.Skip)
            .Take(page.Size)
            .Select(u => new
            {
                u.Id,
                Username = u.Username.Value,
                u.DisplayName,
                Email = u.Email.Value,
                u.IsActive,
                LockoutEnd = EF.Property<DateTimeOffset?>(u, UserCredentialColumns.LockoutEnd),
                RoleIds = u.RoleAssignments.Select(r => r.RoleId).ToList(),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new UserListItem(r.Id, r.Username, r.DisplayName, r.Email, RoleNamesOf(r.RoleIds), StatusOf(r.IsActive, r.LockoutEnd, now)))
            .ToList();
        return PagedResult.Create(items, page, total);
    }

    public async Task<UserDetails?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var row = await context.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                Username = u.Username.Value,
                u.DisplayName,
                Email = u.Email.Value,
                u.Mobile,
                u.IsActive,
                u.MustChangePassword,
                LockoutEnd = EF.Property<DateTimeOffset?>(u, UserCredentialColumns.LockoutEnd),
                RowVersion = EF.Property<byte[]>(u, ShadowColumns.RowVersion),
                RoleIds = u.RoleAssignments.Select(r => r.RoleId).ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new UserDetails(
                row.Id,
                row.Username,
                row.DisplayName,
                row.Email,
                row.Mobile,
                RoleNamesOf(row.RoleIds),
                StatusOf(row.IsActive, row.LockoutEnd, now),
                row.MustChangePassword,
                UserVersion.Encode(row.RowVersion));
    }

    private static List<string> RoleNamesOf(List<Guid> roleIds) =>
        [.. SystemRole.All.Where(role => roleIds.Contains(role.Id)).Select(role => role.Name)];

    private static UserStatus StatusOf(bool isActive, DateTimeOffset? lockoutEnd, DateTimeOffset now) =>
        !isActive ? UserStatus.Inactive
        : lockoutEnd > now ? UserStatus.Locked
        : UserStatus.Active;
}
