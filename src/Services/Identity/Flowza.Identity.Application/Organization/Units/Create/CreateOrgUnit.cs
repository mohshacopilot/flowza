using Flowza.Common.Ids;
using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.Application.Organization.Units.Create;

/// <summary>
/// An admin adds a unit under an active parent, of any active type, at any depth (ORG-02-AC1, ORG-02-AC8, ORG-01-AC4, O1).
/// The code never changes afterwards (ORG-03-AC1).
/// </summary>
public sealed record CreateOrgUnitCommand(Guid? ParentId, string? Code, string? Name, Guid? UnitTypeId);

public sealed record CreatedOrgUnit(Guid Id);

public sealed class CreateOrgUnitHandler(
    IOrgUnitRepository units,
    IUnitTypeRepository unitTypes,
    IOrgStructureRepository structure,
    IUnitNumberSource numbers,
    IUnitOfWork unitOfWork,
    SequentialGuidGenerator ids,
    TimeProvider timeProvider)
{
    public async Task<Result<CreatedOrgUnit>> Handle(CreateOrgUnitCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Codes are stored in upper case, so this also finds the same code in another letter case (ORG-03-AC3). A race
        // past this check ends at the unique index with the same error (unit of work).
        var code = OrgCode.Create(command.Code);
        if (code.IsFailure)
        {
            return code.Error;
        }

        if (await units.IsCodeTakenAsync(code.Value, cancellationToken))
        {
            return OrgErrors.UnitCodeTaken;
        }

        var parent = command.ParentId is { } parentId ? await units.GetAsync(parentId, cancellationToken) : null;
        if (parent is null)
        {
            return OrgErrors.ParentUnknown;
        }

        var unitType = command.UnitTypeId is { } unitTypeId ? await unitTypes.GetAsync(unitTypeId, cancellationToken) : null;
        if (unitType is null)
        {
            return OrgErrors.UnitTypeUnknown;
        }

        // A structural change (plan §5.1): the parent's state and the unit type's state are only trusted together with
        // the org-structure row, so a parent or type deactivated at the same moment ends in a conflict for one of them.
        var orgStructure = await structure.GetAsync(cancellationToken);
        var created = OrgUnit.Create(ids.NewGuid(), await numbers.NextAsync(cancellationToken), code.Value.Value, command.Name, unitType, parent);
        if (created.IsFailure)
        {
            return created.Error;
        }

        units.Add(created.Value);
        orgStructure.Touch(timeProvider.GetUtcNow());
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? new CreatedOrgUnit(created.Value.Id) : saved.Error;
    }
}
