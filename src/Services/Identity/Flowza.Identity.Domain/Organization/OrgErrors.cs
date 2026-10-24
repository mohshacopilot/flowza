using Flowza.Common.Results;

namespace Flowza.Identity.Domain.Organization;

/// <summary>
/// Expected failures of the organization rules (spec 002). Codes are stable translation keys for the web app; messages
/// are English fallbacks and never quote the value that was entered (constitution 11.2).
/// </summary>
public static class OrgErrors
{
    public static Error InvalidCode { get; } = Field(
        "code",
        "org.code_invalid",
        $"A code has 1–{OrgCode.MaxLength} characters: letters, digits, '-' or '_'.");

    public static Error InvalidName { get; } = Field(
        "name",
        "org.name_invalid",
        $"A name is required and has at most {OrgName.MaxLength} characters.");

    /// <summary>P9: a unit type stays active while active units use it.</summary>
    public static Error UnitTypeInUse { get; } = Error.Conflict(
        "org.unit_type_in_use",
        "This unit type is used by active units. Change or deactivate those units first.");

    private static Error Field(string field, string code, string message) =>
        Error.Validation(code, message, new Dictionary<string, string[]> { [field] = [code] });
}
