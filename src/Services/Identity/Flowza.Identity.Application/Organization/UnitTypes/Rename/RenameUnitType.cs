using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.Application.Organization.UnitTypes.Rename;

/// <summary>
/// An admin renames a unit type; the code is not part of the command, it never changes (ORG-01-AC2, O9).
/// <see cref="Version"/> is the version the admin saw (constitution 8.5).
/// </summary>
public sealed record RenameUnitTypeCommand(Guid Id, string? Name, string? Version);

public sealed class RenameUnitTypeHandler(IUnitTypeRepository unitTypes, IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(RenameUnitTypeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var version = RecordVersion.Decode(command.Version);
        if (version is null)
        {
            return RecordVersion.Required;
        }

        var unitType = await unitTypes.GetAsync(command.Id, cancellationToken);
        if (unitType is null)
        {
            return OrgErrors.UnitTypeNotFound;
        }

        var renamed = unitType.Rename(command.Name);
        if (renamed.IsFailure)
        {
            return renamed;
        }

        unitTypes.SetExpectedVersion(unitType, version);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
