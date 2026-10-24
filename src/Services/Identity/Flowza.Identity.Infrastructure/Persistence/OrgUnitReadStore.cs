using Flowza.Common.Paging;
using Flowza.Identity.Application;
using Flowza.Identity.Application.Organization.Units;
using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Identity.Infrastructure.Persistence;

/// <summary>
/// No-tracking projections for the org chart screen (CQRS-lite, T3 D4). The tree is one ordered read of the units with
/// their type and head (O(n), ordered on the path index); details and users are index seeks.
/// </summary>
internal sealed class OrgUnitReadStore(IdentityServiceDbContext context) : IOrgUnitReadStore
{
    public async Task<IReadOnlyList<OrgTreeItem>> GetTreeAsync(CancellationToken cancellationToken)
    {
        // A parent's path is a prefix of its children's paths, so ordering by path puts every parent first.
        var rows = await (
                from unit in context.OrgUnits.AsNoTracking()
                join type in context.UnitTypes.AsNoTracking() on unit.UnitTypeId equals type.Id
                join head in context.Users.AsNoTracking() on unit.HeadUserId equals head.Id into heads
                from head in heads.DefaultIfEmpty()
                orderby unit.Path
                select new
                {
                    unit.Id,
                    unit.ParentId,
                    Code = unit.Code.Value,
                    unit.Name,
                    unit.UnitTypeId,
                    UnitTypeName = type.Name,
                    unit.HeadUserId,
                    HeadDisplayName = head == null ? null : head.DisplayName,
                    unit.IsActive,
                    RowVersion = EF.Property<byte[]>(unit, ShadowColumns.RowVersion),
                })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new OrgTreeItem(
            r.Id, r.ParentId, r.Code, r.Name, r.UnitTypeId, r.UnitTypeName, r.HeadUserId, r.HeadDisplayName, r.IsActive, RecordVersion.Encode(r.RowVersion)))];
    }

    public async Task<OrgUnitDetails?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await (
                from unit in context.OrgUnits.AsNoTracking()
                where unit.Id == id
                join type in context.UnitTypes.AsNoTracking() on unit.UnitTypeId equals type.Id
                join head in context.Users.AsNoTracking() on unit.HeadUserId equals head.Id into heads
                from head in heads.DefaultIfEmpty()
                select new
                {
                    unit.Id,
                    unit.ParentId,
                    Code = unit.Code.Value,
                    unit.Name,
                    unit.UnitTypeId,
                    UnitTypeCode = type.Code.Value,
                    UnitTypeName = type.Name,
                    unit.HeadUserId,
                    HeadDisplayName = head == null ? null : head.DisplayName,
                    HeadUsername = head == null ? null : head.Username.Value,
                    unit.IsActive,
                    ActiveUsers = context.Users.Count(u => u.PrimaryUnitId == unit.Id && u.IsActive),
                    RowVersion = EF.Property<byte[]>(unit, ShadowColumns.RowVersion),
                })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new OrgUnitDetails(
                row.Id,
                row.ParentId,
                row.Code,
                row.Name,
                row.UnitTypeId,
                row.UnitTypeCode,
                row.UnitTypeName,
                row.HeadUserId,
                row.HeadDisplayName,
                row.HeadUsername,
                row.IsActive,
                row.ActiveUsers,
                RecordVersion.Encode(row.RowVersion));
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.OrgUnits.AnyAsync(u => u.Id == id, cancellationToken);

    public async Task<PagedResult<UnitUserItem>> ListUsersAsync(Guid unitId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        var users = context.Users.AsNoTracking().Where(u => u.PrimaryUnitId == unitId);
        var total = await users.CountAsync(cancellationToken);
        var items = await users
            .OrderBy(u => u.NormalizedUsername)
            .Skip(page.Skip)
            .Take(page.Size)
            .Select(u => new UnitUserItem(u.Id, u.Username.Value, u.DisplayName, u.IsActive))
            .ToListAsync(cancellationToken);
        return PagedResult.Create(items, page, total);
    }
}
