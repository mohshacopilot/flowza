using Flowza.Identity.Application;
using Flowza.Identity.Application.Organization.UnitTypes;
using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Identity.Infrastructure.Persistence;

/// <summary>
/// No-tracking projection for the unit types screen (CQRS-lite, T3 D4). One query: each type with a correlated count
/// of its active units (an index seek on the units' type per row; a handful of types).
/// </summary>
internal sealed class UnitTypeReadStore(IdentityServiceDbContext context) : IUnitTypeReadStore
{
    public async Task<IReadOnlyList<UnitTypeListItem>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await context.UnitTypes.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new
            {
                t.Id,
                Code = t.Code.Value,
                t.Name,
                t.IsActive,
                ActiveUnits = context.OrgUnits.Count(u => u.UnitTypeId == t.Id && u.IsActive),
                RowVersion = EF.Property<byte[]>(t, ShadowColumns.RowVersion),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new UnitTypeListItem(r.Id, r.Code, r.Name, r.IsActive, r.ActiveUnits, RecordVersion.Encode(r.RowVersion)))];
    }
}
