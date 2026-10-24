namespace Flowza.Identity.Application.Organization.UnitTypes.List;

/// <summary>The unit types screen: every type with its number of active units (ORG-01, P9).</summary>
public sealed record ListUnitTypesQuery;

public sealed class ListUnitTypesHandler(IUnitTypeReadStore unitTypes)
{
    public Task<IReadOnlyList<UnitTypeListItem>> Handle(ListUnitTypesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return unitTypes.ListAsync(cancellationToken);
    }
}
