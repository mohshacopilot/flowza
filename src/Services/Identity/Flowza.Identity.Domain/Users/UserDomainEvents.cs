using Flowza.Common.Domain;

namespace Flowza.Identity.Domain.Users;

// Domain events of the User aggregate (plan 001 §5.1). The application layer turns them into the integration events
// of plan §7, which carry IDs only.

public sealed record UserCreatedDomainEvent(Guid UserId) : IDomainEvent;

public sealed record UserUpdatedDomainEvent(Guid UserId) : IDomainEvent;

public sealed record UserRolesChangedDomainEvent(Guid UserId, IReadOnlyList<string> Roles) : IDomainEvent;

public sealed record UserDeactivatedDomainEvent(Guid UserId) : IDomainEvent;

public sealed record UserActivatedDomainEvent(Guid UserId) : IDomainEvent;

public sealed record UserPasswordChangedDomainEvent(Guid UserId) : IDomainEvent;

public sealed record UserPasswordResetDomainEvent(Guid UserId) : IDomainEvent;

/// <summary>The user's primary unit or reporting manager changed (plan 002 §6).</summary>
public sealed record UserOrgPlacementChangedDomainEvent(Guid UserId, Guid PrimaryUnitId, Guid? ManagerId) : IDomainEvent;

/// <summary>The user's business roles now are <paramref name="BusinessRoleIds"/> (plan 002 §6).</summary>
public sealed record UserBusinessRolesChangedDomainEvent(Guid UserId, IReadOnlyList<Guid> BusinessRoleIds) : IDomainEvent;
