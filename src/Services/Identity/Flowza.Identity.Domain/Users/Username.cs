using Flowza.Common.Results;

namespace Flowza.Identity.Domain.Users;

/// <summary>
/// The login name (IAM-04, C4): 3–50 characters, ASCII letters, digits, <c>.</c> <c>_</c> <c>-</c>; unique ignoring
/// letter case and never changed after creation (L1, ORG P8). ASCII only, so look-alike letters from other scripts
/// cannot imitate another user's name.
/// </summary>
public sealed record Username
{
    public const int MinLength = 3;
    public const int MaxLength = 50;

    private Username(string value)
    {
        Value = value;
        Normalized = Normalize(value);
    }

    public string Value { get; }

    /// <summary>Upper-case form used for the case-insensitive unique index (C4).</summary>
    public string Normalized { get; }

    public static Result<Username> Create(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is < MinLength or > MaxLength || !trimmed.All(IsAllowed))
        {
            return UserErrors.InvalidUsername;
        }

        return new Username(trimmed);
    }

    /// <summary>Upper-case form of any typed name, for look-ups (e.g. at login).</summary>
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Trim().ToUpperInvariant();
    }

    /// <summary>Rebuilds a value that was valid when it was stored (persistence only).</summary>
    public static Username FromStored(string value) => new(value);

    public override string ToString() => Value;

    private static bool IsAllowed(char c) => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-';
}
