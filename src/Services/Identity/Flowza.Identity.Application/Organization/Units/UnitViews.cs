using Flowza.Common.Paging;

namespace Flowza.Identity.Application.Organization.Units;

/// <summary>
/// One unit of the org chart (plan 002 §5.4). The tree comes as a flat list ordered by path, so every unit comes after
/// its parent and the screen builds the tree in one pass (a dictionary by ID, O(n)).
/// </summary>
public sealed record OrgTreeItem(
    Guid Id,
    Guid? ParentId,
    string Code,
    string Name,
    Guid UnitTypeId,
    string UnitTypeName,
    Guid? HeadUserId,
    string? HeadDisplayName,
    bool IsActive,
    string Version);

/// <summary>A unit's details card: type, head and the number of active users whose primary unit it is.</summary>
public sealed record OrgUnitDetails(
    Guid Id,
    Guid? ParentId,
    string Code,
    string Name,
    Guid UnitTypeId,
    string UnitTypeCode,
    string UnitTypeName,
    Guid? HeadUserId,
    string? HeadDisplayName,
    string? HeadUsername,
    bool IsActive,
    int ActiveUsers,
    string Version);

/// <summary>A user whose primary unit is the unit (active or not).</summary>
public sealed record UnitUserItem(Guid Id, string Username, string DisplayName, bool IsActive);

/// <summary>Read side of org units (CQRS-lite): no-tracking projections shaped for the org chart screen.</summary>
public interface IOrgUnitReadStore
{
    /// <summary>The whole tree. One ordered read of the units, O(n).</summary>
    Task<IReadOnlyList<OrgTreeItem>> GetTreeAsync(CancellationToken cancellationToken);

    Task<OrgUnitDetails?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Users of the unit by username, one page (index seek on the primary unit).</summary>
    Task<PagedResult<UnitUserItem>> ListUsersAsync(Guid unitId, PageRequest page, CancellationToken cancellationToken);
}
