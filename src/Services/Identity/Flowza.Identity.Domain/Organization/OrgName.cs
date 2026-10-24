using Flowza.Common.Results;

namespace Flowza.Identity.Domain.Organization;

/// <summary>The display name of a unit type, an org unit or a business role: required, at most 100 characters.</summary>
public static class OrgName
{
    public const int MaxLength = 100;

    public static Result<string> Clean(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length is 0 or > MaxLength ? OrgErrors.InvalidName : trimmed;
    }
}
