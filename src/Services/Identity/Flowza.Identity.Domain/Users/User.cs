using Flowza.Common.Data;
using Flowza.Common.Domain;
using Flowza.Common.Results;
using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.Domain.Users;

/// <summary>
/// A person who can log in (plan 001 §5.1; spec key entity "User"). Holds the rules that do not depend on how
/// passwords are stored: username never changes (C4), email is required (C3), several system roles with User always
/// present (IAM-02, L2), the last active Platform Admin is protected (L3), re-activation is allowed (C6).
/// From feature 002 (spec 001 amendment 1) also the user's place in the organization: one active primary unit
/// (ORG-04, C2), an optional reporting manager without loops, and business roles (ORG-05).
/// Credentials (password hash, security stamp, lockout counters) live in the infrastructure layer.
/// Users are deactivated, never deleted (constitution 8.6).
/// </summary>
public sealed class User : AggregateRoot, IAuditable, IVersioned
{
    public const int DisplayNameMaxLength = 100;
    public const int MobileMaxLength = 20;

    private readonly List<UserRoleAssignment> _roleAssignments = [];
    private readonly List<UserBusinessRoleAssignment> _businessRoleAssignments = [];

    private User(Guid id, Username username, string displayName, Email email, string? mobile, Guid primaryUnitId)
    {
        Id = id;
        Username = username;
        NormalizedUsername = username.Normalized;
        DisplayName = displayName;
        Email = email;
        NormalizedEmail = email.Normalized;
        Mobile = mobile;
        PrimaryUnitId = primaryUnitId;
    }

    // For EF Core.
    private User()
    {
        Username = null!;
        Email = null!;
        DisplayName = string.Empty;
        NormalizedUsername = string.Empty;
        NormalizedEmail = string.Empty;
    }

    public Guid Id { get; private set; }

    public Username Username { get; private set; }

    public string NormalizedUsername { get; private set; }

    public string DisplayName { get; private set; }

    public Email Email { get; private set; }

    public string NormalizedEmail { get; private set; }

    public string? Mobile { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Set for new users, after an admin reset and for the first admin (IAM-03, IAM-04-AC2).</summary>
    public bool MustChangePassword { get; private set; }

    /// <summary>When the user last chose their own password (UTC); used for expiry in M3 (IAM-06-AC2).</summary>
    public DateTimeOffset? PasswordChangedAt { get; private set; }

    /// <summary>The one unit the user belongs to: "where" the user is for every approver rule (O7, ORG-04-AC1).</summary>
    public Guid PrimaryUnitId { get; private set; }

    /// <summary>The user's reporting manager, a line separate from the unit tree; optional (ORG-04-AC5).</summary>
    public Guid? ManagerId { get; private set; }

    public IReadOnlyCollection<UserRoleAssignment> RoleAssignments => _roleAssignments;

    public IReadOnlyCollection<UserBusinessRoleAssignment> BusinessRoleAssignments => _businessRoleAssignments;

    public IReadOnlyList<Guid> BusinessRoleIds => [.. _businessRoleAssignments.Select(a => a.BusinessRoleId)];

    public IReadOnlyList<SystemRole> Roles =>
        [.. SystemRole.All.Where(role => _roleAssignments.Exists(a => a.RoleId == role.Id))];

    public bool IsActivePlatformAdmin => IsActive && HasRole(SystemRole.PlatformAdmin);

    /// <summary>
    /// Creates an active user who must set a password at first login. The User role is always added (IAM-02). The user
    /// belongs to <paramref name="primaryUnit"/>, which must be active (C2, ORG-04-AC6, spec 001 amendment 1).
    /// </summary>
    public static Result<User> Create(
        Guid id,
        string? username,
        string? displayName,
        string? email,
        string? mobile,
        IEnumerable<SystemRole> roles,
        OrgUnit primaryUnit)
    {
        ArgumentNullException.ThrowIfNull(primaryUnit);
        return primaryUnit.IsActive
            ? Create(id, username, displayName, email, mobile, roles, primaryUnit.Id)
            : OrgErrors.PrimaryUnitInactive;
    }

    /// <summary>
    /// The feature 001 form: the user goes into the installed root unit, the same as the migration does for existing
    /// users. Kept only until the use cases pass a unit (task T010 of feature 002 removes it).
    /// </summary>
    public static Result<User> Create(
        Guid id,
        string? username,
        string? displayName,
        string? email,
        string? mobile,
        IEnumerable<SystemRole> roles) =>
        Create(id, username, displayName, email, mobile, roles, OrgRoot.UnitId);

    private static Result<User> Create(
        Guid id,
        string? username,
        string? displayName,
        string? email,
        string? mobile,
        IEnumerable<SystemRole> roles,
        Guid primaryUnitId)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var name = Username.Create(username);
        if (name.IsFailure)
        {
            return name.Error;
        }

        var profile = ValidateProfile(displayName, email, mobile);
        if (profile.IsFailure)
        {
            return profile.Error;
        }

        var (cleanDisplayName, validEmail, cleanMobile) = profile.Value;
        var user = new User(id, name.Value, cleanDisplayName, validEmail, cleanMobile, primaryUnitId)
        {
            IsActive = true,
            MustChangePassword = true,
        };
        user.ReplaceRoles(roles);
        user.Raise(new UserCreatedDomainEvent(id));
        return user;
    }

    /// <summary>Changes display name, email and mobile. The username cannot change (IAM-04-AC1, C4).</summary>
    public Result UpdateProfile(string? displayName, string? email, string? mobile)
    {
        var profile = ValidateProfile(displayName, email, mobile);
        if (profile.IsFailure)
        {
            return profile.Error;
        }

        var (cleanDisplayName, validEmail, cleanMobile) = profile.Value;
        if (cleanDisplayName == DisplayName && validEmail == Email && cleanMobile == Mobile)
        {
            return Result.Success();
        }

        DisplayName = cleanDisplayName;
        Email = validEmail;
        NormalizedEmail = validEmail.Normalized;
        Mobile = cleanMobile;
        Raise(new UserUpdatedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Sets the system roles (L2). User is always kept (IAM-02). The last active Platform Admin cannot lose the
    /// Platform Admin role (L3).
    /// </summary>
    /// <param name="roles">The new roles.</param>
    /// <param name="activePlatformAdminCount">Active Platform Admins in the installation, this user included.</param>
    public Result SetRoles(IEnumerable<SystemRole> roles, int activePlatformAdminCount)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var wanted = roles.Append(SystemRole.User).ToHashSet();

        if (IsActivePlatformAdmin && !wanted.Contains(SystemRole.PlatformAdmin) && activePlatformAdminCount <= 1)
        {
            return UserErrors.LastActivePlatformAdmin;
        }

        if (wanted.SetEquals(Roles))
        {
            return Result.Success();
        }

        ReplaceRoles(wanted);
        Raise(new UserRolesChangedDomainEvent(Id, [.. Roles.Select(r => r.Name)]));
        return Result.Success();
    }

    /// <summary>Deactivates the user; their sessions end (IAM-04-AC3). The last active Platform Admin is protected (L3).</summary>
    /// <param name="activePlatformAdminCount">Active Platform Admins in the installation, this user included.</param>
    public Result Deactivate(int activePlatformAdminCount)
    {
        if (!IsActive)
        {
            return Result.Success();
        }

        if (IsActivePlatformAdmin && activePlatformAdminCount <= 1)
        {
            return UserErrors.LastActivePlatformAdmin;
        }

        IsActive = false;
        Raise(new UserDeactivatedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Re-activation is allowed; roles and history stay as they were (IAM-04-AC5, C6). The primary unit must be active
    /// first (C2, ORG-04-AC6): the admin sets an active unit with <see cref="PlaceInUnit"/>, then activates.
    /// </summary>
    /// <param name="primaryUnit">The user's own primary unit.</param>
    public Result Activate(OrgUnit primaryUnit)
    {
        ArgumentNullException.ThrowIfNull(primaryUnit);
        if (primaryUnit.Id != PrimaryUnitId)
        {
            throw new ArgumentException("The user's own primary unit must be passed.", nameof(primaryUnit));
        }

        if (IsActive)
        {
            return Result.Success();
        }

        return primaryUnit.IsActive ? Activate() : OrgErrors.PrimaryUnitInactive;
    }

    /// <summary>The feature 001 form without the unit check; kept only until task T010 of feature 002 removes it.</summary>
    public Result Activate()
    {
        if (IsActive)
        {
            return Result.Success();
        }

        IsActive = true;
        Raise(new UserActivatedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Moves the user to another primary unit, which must be active (ORG-04-AC1). Allowed for inactive users too, so an
    /// admin can give a deactivated user an active unit before re-activating them (C2).
    /// </summary>
    public Result PlaceInUnit(OrgUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (!unit.IsActive)
        {
            return OrgErrors.PrimaryUnitInactive;
        }

        if (unit.Id == PrimaryUnitId)
        {
            return Result.Success();
        }

        PrimaryUnitId = unit.Id;
        Raise(new UserOrgPlacementChangedDomainEvent(Id, PrimaryUnitId, ManagerId));
        return Result.Success();
    }

    /// <summary>
    /// Sets the reporting manager, or removes it with null (ORG-04-AC5). Not the user themselves (ORG-04-AC3), an active
    /// user (C7, ORG-04-AC7), and no loop (ORG-04-AC2): the user must not appear among the managers above the new
    /// manager. A broken chain is treated as a loop. O(chain length).
    /// </summary>
    /// <param name="manager">The new manager, or null.</param>
    /// <param name="chainAboveManager">The managers above <paramref name="manager"/> (its <see cref="ManagerChain"/>).</param>
    public Result SetManager(User? manager, ManagerChain? chainAboveManager)
    {
        if (manager is null)
        {
            return ChangeManager(null);
        }

        ArgumentNullException.ThrowIfNull(chainAboveManager);
        if (chainAboveManager.UserId != manager.Id)
        {
            throw new ArgumentException("The chain must start at the new manager.", nameof(chainAboveManager));
        }

        if (manager.Id == Id)
        {
            return OrgErrors.ManagerIsSelf;
        }

        if (!manager.IsActive)
        {
            return OrgErrors.ManagerInactive;
        }

        if (chainAboveManager.IsBroken || chainAboveManager.Contains(Id))
        {
            return OrgErrors.ReportingLoop;
        }

        return ChangeManager(manager.Id);
    }

    /// <summary>
    /// Gives the user a business role (ORG-05-AC1). The user must be active (C7, ORG-05-AC5) and the role active
    /// (P10, ORG-05-AC2). Giving a role the user already holds changes nothing.
    /// </summary>
    public Result AddBusinessRole(BusinessRole role)
    {
        ArgumentNullException.ThrowIfNull(role);
        if (HoldsBusinessRole(role.Id))
        {
            return Result.Success();
        }

        var allowed = CanReceive(role);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        _businessRoleAssignments.Add(new UserBusinessRoleAssignment(role.Id));
        RaiseBusinessRolesChanged();
        return Result.Success();
    }

    /// <summary>Takes a business role away (ORG-05-AC1); also from inactive users and for deactivated roles.</summary>
    public Result RemoveBusinessRole(Guid businessRoleId)
    {
        if (_businessRoleAssignments.RemoveAll(a => a.BusinessRoleId == businessRoleId) == 0)
        {
            return Result.Success();
        }

        RaiseBusinessRolesChanged();
        return Result.Success();
    }

    /// <summary>
    /// Sets all business roles at once (the user screen, ORG-04-AC5). Roles the user already holds may stay even when
    /// deactivated; every newly given role must be active and the user active (C7, P10). All or nothing.
    /// </summary>
    public Result SetBusinessRoles(IReadOnlyCollection<BusinessRole> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var wanted = roles.Select(r => r.Id).ToHashSet();
        foreach (var added in roles.Where(r => !HoldsBusinessRole(r.Id)))
        {
            var allowed = CanReceive(added);
            if (allowed.IsFailure)
            {
                return allowed;
            }
        }

        if (wanted.SetEquals(BusinessRoleIds))
        {
            return Result.Success();
        }

        _businessRoleAssignments.RemoveAll(a => !wanted.Contains(a.BusinessRoleId));
        foreach (var id in wanted.Where(id => !HoldsBusinessRole(id)))
        {
            _businessRoleAssignments.Add(new UserBusinessRoleAssignment(id));
        }

        RaiseBusinessRolesChanged();
        return Result.Success();
    }

    public bool HoldsBusinessRole(Guid businessRoleId) => _businessRoleAssignments.Exists(a => a.BusinessRoleId == businessRoleId);

    private Result CanReceive(BusinessRole role)
    {
        if (!IsActive)
        {
            return OrgErrors.MemberInactive;
        }

        return role.IsActive ? Result.Success() : OrgErrors.BusinessRoleInactive;
    }

    private Result ChangeManager(Guid? managerId)
    {
        if (managerId == ManagerId)
        {
            return Result.Success();
        }

        ManagerId = managerId;
        Raise(new UserOrgPlacementChangedDomainEvent(Id, PrimaryUnitId, ManagerId));
        return Result.Success();
    }

    private void RaiseBusinessRolesChanged() => Raise(new UserBusinessRolesChangedDomainEvent(Id, BusinessRoleIds));

    /// <summary>The user chose a new password themselves (IAM-06-AC3, IAM-03).</summary>
    public void PasswordChanged(DateTimeOffset changedAt)
    {
        MustChangePassword = false;
        PasswordChangedAt = changedAt.ToUniversalTime();
        Raise(new UserPasswordChangedDomainEvent(Id));
    }

    /// <summary>An admin gave the user a temporary password; it must be changed at the next login (IAM-04-AC2, K6).</summary>
    public void PasswordReset()
    {
        MustChangePassword = true;
        Raise(new UserPasswordResetDomainEvent(Id));
    }

    public bool HasRole(SystemRole role) => _roleAssignments.Exists(a => a.RoleId == role.Id);

    private void ReplaceRoles(IEnumerable<SystemRole> roles)
    {
        var wanted = roles.Append(SystemRole.User).Select(r => r.Id).ToHashSet();
        _roleAssignments.RemoveAll(a => !wanted.Contains(a.RoleId));
        foreach (var roleId in wanted.Where(id => !_roleAssignments.Exists(a => a.RoleId == id)))
        {
            _roleAssignments.Add(new UserRoleAssignment(roleId));
        }
    }

    private static Result<(string DisplayName, Email Email, string? Mobile)> ValidateProfile(
        string? displayName,
        string? email,
        string? mobile)
    {
        var cleanDisplayName = displayName?.Trim() ?? string.Empty;
        if (cleanDisplayName.Length is 0 or > DisplayNameMaxLength)
        {
            return UserErrors.InvalidDisplayName;
        }

        var validEmail = Email.Create(email);
        if (validEmail.IsFailure)
        {
            return validEmail.Error;
        }

        var cleanMobile = string.IsNullOrWhiteSpace(mobile) ? null : mobile.Trim();
        if (cleanMobile is not null && (cleanMobile.Length > MobileMaxLength || !cleanMobile.All(IsMobileCharacter) || !cleanMobile.Any(char.IsAsciiDigit)))
        {
            return UserErrors.InvalidMobile;
        }

        return (cleanDisplayName, validEmail.Value, cleanMobile);
    }

    private static bool IsMobileCharacter(char c) => char.IsAsciiDigit(c) || c is ' ' or '+' or '-' or '(' or ')';
}

/// <summary>One system role held by a user (stored as a row of the user-roles table).</summary>
public sealed class UserRoleAssignment
{
    public UserRoleAssignment(Guid roleId) => RoleId = roleId;

    public Guid RoleId { get; private set; }
}

/// <summary>One business role held by a user (ORG-05; a row of the user-business-roles table, plan 002 §5.2).</summary>
public sealed class UserBusinessRoleAssignment
{
    public UserBusinessRoleAssignment(Guid businessRoleId) => BusinessRoleId = businessRoleId;

    public Guid BusinessRoleId { get; private set; }
}
