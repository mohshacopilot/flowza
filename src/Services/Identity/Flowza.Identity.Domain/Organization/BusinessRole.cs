using Flowza.Common.Data;
using Flowza.Common.Domain;
using Flowza.Common.Results;

namespace Flowza.Identity.Domain.Organization;

/// <summary>
/// A job function given to users, e.g. a reviewer or an officer (ORG-05; spec key entity "Business role"). It belongs
/// to the user, not to a place (O7). The code never changes (O9). Deactivated, never deleted (NFR-08): a deactivated
/// role cannot be given to users and approver rules ignore it (P10); re-activation is allowed (C3). Members are held
/// by the users themselves.
/// </summary>
public sealed class BusinessRole : AggregateRoot, IAuditable, IVersioned
{
    private BusinessRole(Guid id, OrgCode code, string name)
    {
        Id = id;
        Code = code;
        Name = name;
    }

    // For EF Core.
    private BusinessRole()
    {
        Code = null!;
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public OrgCode Code { get; private set; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public static Result<BusinessRole> Create(Guid id, string? code, string? name)
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

        var role = new BusinessRole(id, validCode.Value, cleanName.Value) { IsActive = true };
        role.Raise(new BusinessRoleCreatedDomainEvent(id));
        return role;
    }

    /// <summary>Changes the name; the code stays (ORG-05-AC1).</summary>
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
        Raise(new BusinessRoleRenamedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>The role can no longer be given, and rule f ignores it (ORG-05-AC2, P10). Members keep it.</summary>
    public Result Deactivate()
    {
        if (!IsActive)
        {
            return Result.Success();
        }

        IsActive = false;
        Raise(new BusinessRoleDeactivatedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>The role can be given again and rule f uses it again (ORG-05-AC5, C3).</summary>
    public Result Activate()
    {
        if (IsActive)
        {
            return Result.Success();
        }

        IsActive = true;
        Raise(new BusinessRoleActivatedDomainEvent(Id));
        return Result.Success();
    }
}
