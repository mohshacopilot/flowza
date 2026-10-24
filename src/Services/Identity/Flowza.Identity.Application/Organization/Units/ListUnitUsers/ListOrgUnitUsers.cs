using Flowza.Common.Paging;
using Flowza.Common.Results;
using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.Application.Organization.Units.ListUnitUsers;

/// <summary>The users whose primary unit this is, paged by 25 (plan §5.4, T2 S7).</summary>
public sealed record ListOrgUnitUsersQuery(Guid UnitId, int Page = 1, int Size = PageRequest.DefaultSize);

public sealed class ListOrgUnitUsersHandler(IOrgUnitReadStore units)
{
    public async Task<Result<PagedResult<UnitUserItem>>> Handle(ListOrgUnitUsersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!await units.ExistsAsync(query.UnitId, cancellationToken))
        {
            return OrgErrors.UnitNotFound;
        }

        return await units.ListUsersAsync(query.UnitId, new PageRequest(query.Page, query.Size), cancellationToken);
    }
}
