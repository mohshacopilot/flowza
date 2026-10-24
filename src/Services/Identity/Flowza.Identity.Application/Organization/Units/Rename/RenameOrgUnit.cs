using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.Application.Organization.Units.Rename;

/// <summary>
/// An admin renames a unit; the code is not part of the command, it never changes (ORG-03-AC1, ORG-03-AC4).
/// Not a structural change: only the unit's own row version guards it (constitution 8.5).
/// </summary>
public sealed record RenameOrgUnitCommand(Guid Id, string? Name, string? Version);

public sealed class RenameOrgUnitHandler(IOrgUnitRepository units, IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(RenameOrgUnitCommand command, CancellationToken cancellationToken)
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

        var renamed = unit.Rename(command.Name);
        if (renamed.IsFailure)
        {
            return renamed;
        }

        units.SetExpectedVersion(unit, version);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
