using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.Application.Organization.UnitTypes.ChangeStatus;

/// <summary>Refused while active units use the type (ORG-01-AC3, P9); existing units keep it (ORG-01-AC4).</summary>
public sealed record DeactivateUnitTypeCommand(Guid Id, string? Version);

/// <summary>The type can be chosen for new units again (ORG-01-AC6, C3).</summary>
public sealed record ActivateUnitTypeCommand(Guid Id, string? Version);

public sealed class DeactivateUnitTypeHandler(
    IUnitTypeRepository unitTypes,
    IOrgStructureRepository structure,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result> Handle(DeactivateUnitTypeCommand command, CancellationToken cancellationToken)
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

        if (!unitType.IsActive)
        {
            return Result.Success();
        }

        // P9 counts the active units; a unit created with this type at the same moment touches the org structure too,
        // so one of the two saves gets the conflict and P9 cannot be passed on stale data (plan §5.1).
        var orgStructure = await structure.GetAsync(cancellationToken);
        var deactivated = unitType.Deactivate(await unitTypes.CountActiveUnitsAsync(unitType.Id, cancellationToken));
        if (deactivated.IsFailure)
        {
            return deactivated;
        }

        orgStructure.Touch(timeProvider.GetUtcNow());
        unitTypes.SetExpectedVersion(unitType, version);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ActivateUnitTypeHandler(IUnitTypeRepository unitTypes, IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(ActivateUnitTypeCommand command, CancellationToken cancellationToken)
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

        unitType.Activate();
        unitTypes.SetExpectedVersion(unitType, version);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
