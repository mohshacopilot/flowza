using Flowza.Common.Results;

namespace Flowza.Identity.Domain.Users;

/// <summary>
/// A user's email address (IAM-04, C3): required, unique ignoring letter case, used for notifications (K1). The
/// format check is deliberately simple (one <c>@</c>, a dotted domain, no spaces); delivery proves the rest.
/// </summary>
public sealed record Email
{
    public const int MaxLength = 256;

    private Email(string value)
    {
        Value = value;
        Normalized = Normalize(value);
    }

    public string Value { get; }

    /// <summary>Upper-case form used for the case-insensitive unique index (C3).</summary>
    public string Normalized { get; }

    public static Result<Email> Create(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return IsValid(trimmed) ? new Email(trimmed) : UserErrors.InvalidEmail;
    }

    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Trim().ToUpperInvariant();
    }

    /// <summary>Rebuilds a value that was valid when it was stored (persistence only).</summary>
    public static Email FromStored(string value) => new(value);

    public override string ToString() => Value;

    private static bool IsValid(string value)
    {
        if (value.Length is 0 or > MaxLength || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1)
        {
            return false;
        }

        var domain = value[(at + 1)..];
        var dot = domain.IndexOf('.', StringComparison.Ordinal);
        return dot > 0 && !domain.EndsWith('.') && !domain.Contains("..", StringComparison.Ordinal);
    }
}
