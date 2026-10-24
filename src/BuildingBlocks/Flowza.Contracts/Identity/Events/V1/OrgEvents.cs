namespace Flowza.Contracts.Identity.Events.V1;

// Integration events of the organization (plan 002 §6, PA3). IDs only: never codes, names or other text
// (constitution 11.2). Each also carries EventId, OccurredAt, ActorId and CorrelationId from IntegrationEvent.
// No service subscribes in v0.1; the Audit service (v0.4) will.

/// <summary>An admin added a unit type.</summary>
public sealed record UnitTypeCreated(Guid UnitTypeId) : IntegrationEvent;

/// <summary>A unit type got a new name; its code never changes.</summary>
public sealed record UnitTypeRenamed(Guid UnitTypeId) : IntegrationEvent;

/// <summary>A unit type can no longer be chosen for new units.</summary>
public sealed record UnitTypeDeactivated(Guid UnitTypeId) : IntegrationEvent;

/// <summary>A unit type can be chosen for new units again.</summary>
public sealed record UnitTypeActivated(Guid UnitTypeId) : IntegrationEvent;

/// <summary>An admin added a unit under <paramref name="ParentId"/>.</summary>
public sealed record OrgUnitCreated(Guid UnitId, Guid ParentId, Guid UnitTypeId) : IntegrationEvent;

/// <summary>A unit got a new name; its code never changes.</summary>
public sealed record OrgUnitRenamed(Guid UnitId) : IntegrationEvent;

/// <summary>The unit's head is now <paramref name="HeadUserId"/>, or nobody (vacant).</summary>
public sealed record OrgUnitHeadChanged(Guid UnitId, Guid? HeadUserId) : IntegrationEvent;

/// <summary>The unit and everything below it moved under another unit.</summary>
public sealed record OrgUnitMoved(Guid UnitId, Guid OldParentId, Guid NewParentId) : IntegrationEvent;

public sealed record OrgUnitDeactivated(Guid UnitId) : IntegrationEvent;

public sealed record OrgUnitActivated(Guid UnitId) : IntegrationEvent;

/// <summary>An admin added a business role.</summary>
public sealed record BusinessRoleCreated(Guid BusinessRoleId) : IntegrationEvent;

/// <summary>A business role got a new name; its code never changes.</summary>
public sealed record BusinessRoleRenamed(Guid BusinessRoleId) : IntegrationEvent;

/// <summary>A business role can no longer be given, and approver rules ignore it.</summary>
public sealed record BusinessRoleDeactivated(Guid BusinessRoleId) : IntegrationEvent;

public sealed record BusinessRoleActivated(Guid BusinessRoleId) : IntegrationEvent;

/// <summary>The user's primary unit or reporting manager changed; both are given as they are now.</summary>
public sealed record UserOrgPlacementChanged(Guid UserId, Guid PrimaryUnitId, Guid? ManagerId) : IntegrationEvent;

/// <summary>The user's business roles now are <paramref name="BusinessRoleIds"/>.</summary>
public sealed record UserBusinessRolesChanged(Guid UserId, IReadOnlyList<Guid> BusinessRoleIds) : IntegrationEvent;
