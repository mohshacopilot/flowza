using Flowza.Common.Results;
using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.Application.Organization.Units.GetUnit;

/// <summary>The details card of one unit (ORG-03): type, head, number of active users.</summary>
public sealed record GetOrgUnitQuery(Guid Id);

public sealed class GetOrgUnitHandler(IOrgUnitReadStore units)
{
    public async Task<Result<OrgUnitDetails>> Handle(GetOrgUnitQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var unit = await units.GetAsync(query.Id, cancellationToken);
        return unit is null ? OrgErrors.UnitNotFound : unit;
    }
}
