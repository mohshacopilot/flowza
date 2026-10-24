namespace Flowza.Identity.Application.Organization.UnitTypes;

/// <summary>
/// One row of the unit types screen (plan 002 §5.4): <see cref="ActiveUnits"/> tells the admin why a deactivation would
/// be refused (P9); <see cref="Version"/> is sent back with a change (constitution 8.5).
/// </summary>
public sealed record UnitTypeListItem(Guid Id, string Code, string Name, bool IsActive, int ActiveUnits, string Version);

/// <summary>Read side of unit types (CQRS-lite): no-tracking projections shaped for the screen.</summary>
public interface IUnitTypeReadStore
{
    /// <summary>All unit types, ordered by name. An installation has a handful, so the list is not paged.</summary>
    Task<IReadOnlyList<UnitTypeListItem>> ListAsync(CancellationToken cancellationToken);
}
