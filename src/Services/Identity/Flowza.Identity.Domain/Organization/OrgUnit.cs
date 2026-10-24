using Flowza.Common.Data;
using Flowza.Common.Domain;
using Flowza.Common.Results;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.Domain.Organization;

/// <summary>
/// One box in the org tree (ORG-02, ORG-03; spec key entity "Org unit"): code (never changes, O9), name, type, parent
/// (none for the root), optional head, active flag. Any type may sit under any other (O1). The tree is an adjacency
/// list (<see cref="ParentId"/>) plus a materialized path (<see cref="Path"/>, plan 002 §8). Deactivated, never deleted
/// (NFR-08); an active unit never sits under an inactive one (C4).
/// </summary>
public sealed class OrgUnit : AggregateRoot, IAuditable, IVersioned
{
    private OrgUnit(Guid id, int number, OrgCode code, string name, Guid unitTypeId, Guid? parentId, UnitPath path)
    {
        Id = id;
        Number = number;
        Code = code;
        Name = name;
        UnitTypeId = unitTypeId;
        ParentId = parentId;
        Path = path;
        IsActive = true;
    }

    // For EF Core.
    private OrgUnit()
    {
        Code = null!;
        Name = string.Empty;
        Path = null!;
    }

    public Guid Id { get; private set; }

    /// <summary>Small internal key used only inside <see cref="Path"/>; unique, never shown.</summary>
    public int Number { get; private set; }

    public OrgCode Code { get; private set; }

    public string Name { get; private set; }

    public Guid UnitTypeId { get; private set; }

    public Guid? ParentId { get; private set; }

    public UnitPath Path { get; private set; }

    /// <summary>The user who leads the unit, or null while it is vacant (O3). May work elsewhere and head other units (O10, O11).</summary>
    public Guid? HeadUserId { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsRoot => ParentId is null;

    /// <summary>
    /// The installation's one root unit (C1). Only the installation creates it (the migration seed); the database allows
    /// a single unit without a parent (ORG-02-AC5).
    /// </summary>
    public static OrgUnit CreateRoot(Guid id, int number, OrgCode code, string name, Guid unitTypeId)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new OrgUnit(id, number, code, name, unitTypeId, parentId: null, UnitPath.ForRoot(number));
    }

    /// <summary>
    /// A new active unit under <paramref name="parent"/>, at any depth up to <see cref="UnitPath.MaxDepth"/> levels and of
    /// any type (ORG-02-AC1, O1). The parent must be active (C4, ORG-02-AC8) and the type active (ORG-01-AC4).
    /// </summary>
    public static Result<OrgUnit> Create(Guid id, int number, string? code, string? name, UnitType unitType, OrgUnit parent)
    {
        ArgumentNullException.ThrowIfNull(unitType);
        ArgumentNullException.ThrowIfNull(parent);

        var validCode = OrgCode.Create(code);
        if (validCode.IsFailure)
        {
            return validCode.Error;
        }

        var cleanName = OrgName.Clean(name);
        if (cleanName.IsFailure)
        {
            return cleanName.Error;
        }

        if (!unitType.IsActive)
        {
            return OrgErrors.UnitTypeInactive;
        }

        if (!parent.IsActive)
        {
            return OrgErrors.ParentInactive;
        }

        var path = parent.Path.Append(number);
        if (path.IsFailure)
        {
            return path.Error;
        }

        var unit = new OrgUnit(id, number, validCode.Value, cleanName.Value, unitType.Id, parent.Id, path.Value);
        unit.Raise(new OrgUnitCreatedDomainEvent(id, parent.Id, unitType.Id));
        return unit;
    }

    /// <summary>Changes the name; the code stays (ORG-03-AC1, ORG-03-AC4).</summary>
    public Result Rename(string? name)
    {
        var cleanName = OrgName.Clean(name);
        if (cleanName.IsFailure)
        {
            return cleanName.Error;
        }

        if (cleanName.Value == Name)
        {
            return Result.Success();
        }

        Name = cleanName.Value;
        Raise(new OrgUnitRenamedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Sets the head, or makes the unit vacant with null (ORG-03-AC4). A new head must be an active user (ORG-03-AC2);
    /// they need not work in the unit (O11) and may head other units (O10).
    /// </summary>
    public Result SetHead(User? head)
    {
        if (head is not null && !head.IsActive)
        {
            return OrgErrors.HeadInactive;
        }

        var headId = head?.Id;
        if (headId == HeadUserId)
        {
            return Result.Success();
        }

        HeadUserId = headId;
        Raise(new OrgUnitHeadChangedDomainEvent(Id, headId));
        return Result.Success();
    }

    /// <summary>
    /// Moves the unit with its whole subtree under <paramref name="newParent"/> (ORG-02-AC3). Refused for the root, under
    /// the unit itself or anywhere in its own subtree (ORG-02-AC2), for an active unit under an inactive one (C4,
    /// ORG-02-AC8), and when the subtree would end deeper than <see cref="UnitPath.MaxDepth"/>. O(subtree size).
    /// </summary>
    /// <param name="newParent">The unit to move under.</param>
    /// <param name="descendants">Every unit below this one (paths starting with this unit's path, this unit left out).</param>
    public Result MoveTo(OrgUnit newParent, IReadOnlyCollection<OrgUnit> descendants)
    {
        ArgumentNullException.ThrowIfNull(newParent);
        ArgumentNullException.ThrowIfNull(descendants);

        if (IsRoot)
        {
            return OrgErrors.RootCannotMove;
        }

        if (newParent.Path.IsSameOrBelow(Path))
        {
            return OrgErrors.MoveIntoOwnSubtree;
        }

        if (IsActive && !newParent.IsActive)
        {
            return OrgErrors.ParentInactive;
        }

        if (newParent.Id == ParentId)
        {
            return Result.Success();
        }

        var newPath = newParent.Path.Append(Number);
        if (newPath.IsFailure)
        {
            return newPath.Error;
        }

        // All new paths are worked out first, so a subtree that would end too deep changes nothing.
        var moved = new List<(OrgUnit Unit, UnitPath Path)>(descendants.Count);
        foreach (var descendant in descendants)
        {
            if (descendant.Id == Id || !descendant.Path.IsSameOrBelow(Path))
            {
                throw new ArgumentException("Only the units below the moved unit may be passed.", nameof(descendants));
            }

            var rebased = descendant.Path.Rebase(Path, newPath.Value);
            if (rebased.IsFailure)
            {
                return rebased.Error;
            }

            moved.Add((descendant, rebased.Value));
        }

        var oldParentId = ParentId!.Value;
        ParentId = newParent.Id;
        Path = newPath.Value;
        moved.ForEach(m => m.Unit.Path = m.Path);
        Raise(new OrgUnitMovedDomainEvent(Id, oldParentId, newParent.Id));
        return Result.Success();
    }

    /// <summary>
    /// Refused while the unit has active child units or active users (O2, ORG-02-AC4); the caller, which counted them,
    /// names them in its answer. Also covers the root (spec edge case).
    /// </summary>
    public Result Deactivate(int activeChildUnits, int activeUsers)
    {
        if (!IsActive)
        {
            return Result.Success();
        }

        if (activeChildUnits > 0 || activeUsers > 0)
        {
            return OrgErrors.UnitHasActiveMembers;
        }

        IsActive = false;
        Raise(new OrgUnitDeactivatedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>Re-activation only while the parent is active (C3, ORG-02-AC9). The root has no parent.</summary>
    /// <param name="parent">The unit's parent; null only for the root.</param>
    public Result Activate(OrgUnit? parent)
    {
        if (parent?.Id != ParentId)
        {
            throw new ArgumentException("The unit's own parent must be passed.", nameof(parent));
        }

        if (IsActive)
        {
            return Result.Success();
        }

        if (parent is { IsActive: false })
        {
            return OrgErrors.ParentInactive;
        }

        IsActive = true;
        Raise(new OrgUnitActivatedDomainEvent(Id));
        return Result.Success();
    }
}
