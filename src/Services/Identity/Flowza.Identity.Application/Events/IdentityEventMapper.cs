using Flowza.Common.Domain;
using Flowza.Contracts;
using Flowza.Contracts.Identity.Events.V1;
using Flowza.Identity.Domain.Organization;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.Application.Events;

/// <summary>
/// Turns the domain events of Identity's aggregates (users, and from feature 002 unit types, org units and business
/// roles) into the integration events that leave the service (T2, S6). The standard fields (time, actor, correlation)
/// are added when they are saved, by the unit of work.
/// </summary>
public static class IdentityEventMapper
{
    public static IntegrationEvent Map(IDomainEvent domainEvent) => domainEvent switch
    {
        UserCreatedDomainEvent e => new UserCreated(e.UserId),
        UserUpdatedDomainEvent e => new UserUpdated(e.UserId),
        UserRolesChangedDomainEvent e => new UserRolesChanged(e.UserId, e.Roles),
        UserDeactivatedDomainEvent e => new UserDeactivated(e.UserId),
        UserActivatedDomainEvent e => new UserActivated(e.UserId),
        UserPasswordChangedDomainEvent e => new UserPasswordChanged(e.UserId),
        UserPasswordResetDomainEvent e => new UserPasswordReset(e.UserId),
        UserOrgPlacementChangedDomainEvent e => new UserOrgPlacementChanged(e.UserId, e.PrimaryUnitId, e.ManagerId),
        UserBusinessRolesChangedDomainEvent e => new UserBusinessRolesChanged(e.UserId, e.BusinessRoleIds),
        UnitTypeCreatedDomainEvent e => new UnitTypeCreated(e.UnitTypeId),
        UnitTypeRenamedDomainEvent e => new UnitTypeRenamed(e.UnitTypeId),
        UnitTypeDeactivatedDomainEvent e => new UnitTypeDeactivated(e.UnitTypeId),
        UnitTypeActivatedDomainEvent e => new UnitTypeActivated(e.UnitTypeId),
        OrgUnitCreatedDomainEvent e => new OrgUnitCreated(e.UnitId, e.ParentId, e.UnitTypeId),
        OrgUnitRenamedDomainEvent e => new OrgUnitRenamed(e.UnitId),
        OrgUnitHeadChangedDomainEvent e => new OrgUnitHeadChanged(e.UnitId, e.HeadUserId),
        OrgUnitMovedDomainEvent e => new OrgUnitMoved(e.UnitId, e.OldParentId, e.NewParentId),
        OrgUnitDeactivatedDomainEvent e => new OrgUnitDeactivated(e.UnitId),
        OrgUnitActivatedDomainEvent e => new OrgUnitActivated(e.UnitId),
        BusinessRoleCreatedDomainEvent e => new BusinessRoleCreated(e.BusinessRoleId),
        BusinessRoleRenamedDomainEvent e => new BusinessRoleRenamed(e.BusinessRoleId),
        BusinessRoleDeactivatedDomainEvent e => new BusinessRoleDeactivated(e.BusinessRoleId),
        BusinessRoleActivatedDomainEvent e => new BusinessRoleActivated(e.BusinessRoleId),
        _ => throw new ArgumentOutOfRangeException(nameof(domainEvent), domainEvent?.GetType().Name, "No integration event for this domain event."),
    };
}
