using Flowza.Common.Domain;
using Flowza.Contracts;
using Flowza.Contracts.Identity.Events.V1;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.Application.Events;

/// <summary>
/// Turns the User aggregate's domain events into the integration events that leave the service (T2, S6). The
/// standard fields (time, actor, correlation) are added when they are saved, by the unit of work.
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
        _ => throw new ArgumentOutOfRangeException(nameof(domainEvent), domainEvent?.GetType().Name, "No integration event for this domain event."),
    };
}
