using Flowza.Common.Data;
using Flowza.Common.Domain;
using Flowza.Common.Results;

namespace Flowza.Identity.Domain.Organization;

/// <summary>
/// A level name defined by the admin, e.g. Head Office, Zone, Branch (ORG-01; spec key entity "Unit type"). The code
/// never changes (O9); the name can. Deactivated, never deleted (NFR-08): a deactivated type cannot be chosen for new
/// units, existing units keep it, and deactivation is refused while active units use it (P9). Re-activation is
/// allowed (C3).
/// </summary>
public sealed class UnitType : AggregateRoot, IAuditable, IVersioned
{
    private UnitType(Guid id, OrgCode code, string name)
    {
        Id = id;
        Code = code;
        Name = name;
    }

    // For EF Core.
    private UnitType()
    {
        Code = null!;
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public OrgCode Code { get; private set; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public static Result<UnitType> Create(Guid id, string? code, string? name)
    {
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

        var unitType = new UnitType(id, validCode.Value, cleanName.Value) { IsActive = true };
        unitType.Raise(new UnitTypeCreatedDomainEvent(id));
        return unitType;
    }

    /// <summary>Changes the name; the code stays (ORG-01-AC2).</summary>
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
        Raise(new UnitTypeRenamedDomainEvent(Id));
        return Result.Success();
    }

    /// <param name="activeUnitsOfType">Active org units of this type (P9: must be 0).</param>
    public Result Deactivate(int activeUnitsOfType)
    {
        if (!IsActive)
        {
            return Result.Success();
        }

        if (activeUnitsOfType > 0)
        {
            return OrgErrors.UnitTypeInUse;
        }

        IsActive = false;
        Raise(new UnitTypeDeactivatedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>A deactivated type can be chosen for new units again (ORG-01-AC6, C3).</summary>
    public Result Activate()
    {
        if (IsActive)
        {
            return Result.Success();
        }

        IsActive = true;
        Raise(new UnitTypeActivatedDomainEvent(Id));
        return Result.Success();
    }
}
