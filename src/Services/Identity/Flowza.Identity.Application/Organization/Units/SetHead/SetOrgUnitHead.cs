using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.Application.Organization.Units.SetHead;

/// <summary>
/// An admin sets, changes or clears (null) a unit's head (ORG-03-AC4). The head must be an active user (ORG-03-AC2); they
/// may work in another unit (O11) and head other units (O10). Not a structural change (plan §5.1).
/// </summary>
public sealed record SetOrgUnitHeadCommand(Guid Id, Guid? HeadUserId, string? Version);

public sealed class SetOrgUnitHeadHandler(IOrgUnitRepository units, IUserRepository users, IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(SetOrgUnitHeadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var version = RecordVersion.Decode(command.Version);
        if (version is null)
        {
            return RecordVersion.Required;
        }

        var unit = await units.GetAsync(command.Id, cancellationToken);
        if (unit is null)
        {
            return OrgErrors.UnitNotFound;
        }

        var head = command.HeadUserId is { } headUserId ? await users.GetAsync(headUserId, cancellationToken) : null;
        if (command.HeadUserId is not null && head is null)
        {
            return OrgErrors.HeadUnknown;
        }

        var changed = unit.SetHead(head);
        if (changed.IsFailure)
        {
            return changed;
        }

        units.SetExpectedVersion(unit, version);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
