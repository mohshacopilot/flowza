using Flowza.Common.Data;
using Flowza.Common.Domain;
using Flowza.Common.Results;

namespace Flowza.Identity.Domain.Users;

/// <summary>
/// A person who can log in (plan 001 §5.1; spec key entity "User"). Holds the rules that do not depend on how
/// passwords are stored: username never changes (C4), email is required (C3), several system roles with User always
/// present (IAM-02, L2), the last active Platform Admin is protected (L3), re-activation is allowed (C6).
/// Credentials (password hash, security stamp, lockout counters) live in the infrastructure layer.
/// Users are deactivated, never deleted (constitution 8.6).
/// </summary>
public sealed class User : AggregateRoot, IAuditable, IVersioned
{
    public const int DisplayNameMaxLength = 100;
    public const int MobileMaxLength = 20;

    private readonly List<UserRoleAssignment> _roleAssignments = [];

    private User(Guid id, Username username, string displayName, Email email, string? mobile)
    {
        Id = id;
        Username = username;
        NormalizedUsername = username.Normalized;
        DisplayName = displayName;
        Email = email;
        NormalizedEmail = email.Normalized;
        Mobile = mobile;
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

    public IReadOnlyCollection<UserRoleAssignment> RoleAssignments => _roleAssignments;

    public IReadOnlyList<SystemRole> Roles =>
        [.. SystemRole.All.Where(role => _roleAssignments.Exists(a => a.RoleId == role.Id))];

    public bool IsActivePlatformAdmin => IsActive && HasRole(SystemRole.PlatformAdmin);

    /// <summary>
    /// Creates an active user who must set a password at first login. The User role is always added (IAM-02).
    /// </summary>
    public static Result<User> Create(
        Guid id,
        string? username,
        string? displayName,
        string? email,
        string? mobile,
        IEnumerable<SystemRole> roles)
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
        var user = new User(id, name.Value, cleanDisplayName, validEmail, cleanMobile)
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

    /// <summary>Re-activation is allowed; roles and history stay as they were (IAM-04-AC5, C6).</summary>
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
