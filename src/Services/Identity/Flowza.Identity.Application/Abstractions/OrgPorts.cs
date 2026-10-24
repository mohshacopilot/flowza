using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.Application.Abstractions;

// Ports for the organization (feature 002, plan §4): the use cases of tasks T006–T010 load and add the org aggregates
// through these; the infrastructure layer implements them with EF Core.

/// <summary>Loads and adds <see cref="UnitType"/> aggregates (tracked, for changes).</summary>
public interface IUnitTypeRepository
{
    Task<UnitType?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> IsCodeTakenAsync(OrgCode code, CancellationToken cancellationToken);

    /// <summary>Active org units of this type (P9). Index seek on the units' type.</summary>
    Task<int> CountActiveUnitsAsync(Guid unitTypeId, CancellationToken cancellationToken);

    void Add(UnitType unitType);

    /// <summary>The row version the client last saw (constitution 8.5).</summary>
    void SetExpectedVersion(UnitType unitType, byte[] rowVersion);
}

/// <summary>Loads and adds <see cref="OrgUnit"/> aggregates (tracked, for changes).</summary>
public interface IOrgUnitRepository
{
    Task<OrgUnit?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> IsCodeTakenAsync(OrgCode code, CancellationToken cancellationToken);

    /// <summary>
    /// Every unit below <paramref name="unit"/> (its path is a prefix of theirs), the unit itself left out, tracked for a
    /// move (plan §5.3). One index range seek on the path, O(subtree size).
    /// </summary>
    Task<IReadOnlyList<OrgUnit>> GetDescendantsAsync(OrgUnit unit, CancellationToken cancellationToken);

    void Add(OrgUnit unit);

    /// <summary>The row version the client last saw (constitution 8.5).</summary>
    void SetExpectedVersion(OrgUnit unit, byte[] rowVersion);
}

/// <summary>Loads and adds <see cref="BusinessRole"/> aggregates (tracked, for changes).</summary>
public interface IBusinessRoleRepository
{
    Task<BusinessRole?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> IsCodeTakenAsync(OrgCode code, CancellationToken cancellationToken);

    void Add(BusinessRole role);

    /// <summary>The row version the client last saw (constitution 8.5).</summary>
    void SetExpectedVersion(BusinessRole role, byte[] rowVersion);
}

/// <summary>
/// The one <see cref="OrgStructure"/> row, tracked: a structural change touches it in its own transaction, so two such
/// changes at the same moment end in a conflict for the second (plan §5.1).
/// </summary>
public interface IOrgStructureRepository
{
    Task<OrgStructure> GetAsync(CancellationToken cancellationToken);
}

/// <summary>Hands out the next unit number for a new unit's path (sequence <c>UnitNumbers</c>, from 2; plan §5.3).</summary>
public interface IUnitNumberSource
{
    /// <summary>A number never handed out before; numbers of failed saves are lost, which is harmless (they are never shown).</summary>
    Task<int> NextAsync(CancellationToken cancellationToken);
}
