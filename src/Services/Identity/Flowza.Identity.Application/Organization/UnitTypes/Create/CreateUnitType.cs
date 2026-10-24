using Flowza.Common.Ids;
using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.Application.Organization.UnitTypes.Create;

/// <summary>An admin adds a level name with a code that never changes afterwards (ORG-01-AC1, ORG-01-AC2, P7).</summary>
public sealed record CreateUnitTypeCommand(string? Code, string? Name);

public sealed record CreatedUnitType(Guid Id);

public sealed class CreateUnitTypeHandler(IUnitTypeRepository unitTypes, IUnitOfWork unitOfWork, SequentialGuidGenerator ids)
{
    public async Task<Result<CreatedUnitType>> Handle(CreateUnitTypeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var created = UnitType.Create(ids.NewGuid(), command.Code, command.Name);
        if (created.IsFailure)
        {
            return created.Error;
        }

        // Codes are stored in upper case, so this also finds the same code in another letter case (ORG-01-AC5). A race
        // past this check ends at the unique index with the same error (unit of work).
        var unitType = created.Value;
        if (await unitTypes.IsCodeTakenAsync(unitType.Code, cancellationToken))
        {
            return OrgErrors.UnitTypeCodeTaken;
        }

        unitTypes.Add(unitType);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? new CreatedUnitType(unitType.Id) : saved.Error;
    }
}
