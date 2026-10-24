namespace Flowza.Identity.Application.Organization.Units.GetTree;

/// <summary>The whole org tree for the org chart screen, as a flat list ordered by path (ORG-02, plan §5.4).</summary>
public sealed record GetOrgTreeQuery;

public sealed class GetOrgTreeHandler(IOrgUnitReadStore units)
{
    public Task<IReadOnlyList<OrgTreeItem>> Handle(GetOrgTreeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return units.GetTreeAsync(cancellationToken);
    }
}
