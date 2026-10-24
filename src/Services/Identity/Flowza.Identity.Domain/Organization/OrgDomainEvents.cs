using Flowza.Common.Domain;

namespace Flowza.Identity.Domain.Organization;

// Domain events of the organization aggregates (plan 002 §6). The application layer turns them into integration
// events, which carry IDs only.

public sealed record UnitTypeCreatedDomainEvent(Guid UnitTypeId) : IDomainEvent;

public sealed record UnitTypeRenamedDomainEvent(Guid UnitTypeId) : IDomainEvent;

public sealed record UnitTypeDeactivatedDomainEvent(Guid UnitTypeId) : IDomainEvent;

public sealed record UnitTypeActivatedDomainEvent(Guid UnitTypeId) : IDomainEvent;

public sealed record OrgUnitCreatedDomainEvent(Guid UnitId, Guid ParentId, Guid UnitTypeId) : IDomainEvent;

public sealed record OrgUnitRenamedDomainEvent(Guid UnitId) : IDomainEvent;

public sealed record OrgUnitHeadChangedDomainEvent(Guid UnitId, Guid? HeadUserId) : IDomainEvent;

public sealed record OrgUnitMovedDomainEvent(Guid UnitId, Guid OldParentId, Guid NewParentId) : IDomainEvent;

public sealed record OrgUnitDeactivatedDomainEvent(Guid UnitId) : IDomainEvent;

public sealed record OrgUnitActivatedDomainEvent(Guid UnitId) : IDomainEvent;

public sealed record BusinessRoleCreatedDomainEvent(Guid BusinessRoleId) : IDomainEvent;

public sealed record BusinessRoleRenamedDomainEvent(Guid BusinessRoleId) : IDomainEvent;

public sealed record BusinessRoleDeactivatedDomainEvent(Guid BusinessRoleId) : IDomainEvent;

public sealed record BusinessRoleActivatedDomainEvent(Guid BusinessRoleId) : IDomainEvent;
