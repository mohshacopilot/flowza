using Flowza.Common.Results;

namespace Flowza.Identity.Domain.Organization;

/// <summary>
/// The fixed identifier of a unit type, an org unit or a business role (spec 002 key entity "Code"; P7, O9): 1–50
/// characters, ASCII letters, digits, <c>-</c> and <c>_</c>. It is stored in upper case, so two codes that differ only
/// in letter case are the same code, and a plain unique index enforces "unique ignoring case". Process designs refer
/// to it, so it never changes after creation.
/// </summary>
public sealed record OrgCode
{
    public const int MaxLength = 50;

    private OrgCode(string value) => Value = value;

    /// <summary>The code in upper case.</summary>
    public string Value { get; }

    public static Result<OrgCode> Create(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxLength || !trimmed.All(IsAllowed))
        {
            return OrgErrors.InvalidCode;
        }

        return new OrgCode(trimmed.ToUpperInvariant());
    }

    /// <summary>Rebuilds a value that was valid when it was stored (persistence only).</summary>
    public static OrgCode FromStored(string value) => new(value);

    public override string ToString() => Value;

    // ASCII only, like usernames: look-alike letters from other scripts cannot imitate another code.
    private static bool IsAllowed(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_';
}
