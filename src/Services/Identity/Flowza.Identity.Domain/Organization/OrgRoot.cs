namespace Flowza.Identity.Domain.Organization;

/// <summary>
/// The root unit and root unit type that every installation starts with (C1, PA5). The migration seeds both with these
/// fixed IDs and codes; the admin renames them, the codes never change (O9).
/// </summary>
public static class OrgRoot
{
    public static readonly Guid UnitTypeId = new("0f6e5600-0000-4000-8000-000000000002");

    public static readonly Guid UnitId = new("0f6e5600-0000-4000-8000-000000000003");

    /// <summary>The code of both the root unit type and the root unit.</summary>
    public const string Code = "ROOT";

    /// <summary>The installed name of both; the admin renames them.</summary>
    public const string Name = "Organization";

    /// <summary>The root unit's number in the materialized path (<c>/1/</c>); new units start at 2.</summary>
    public const int UnitNumber = 1;
}
