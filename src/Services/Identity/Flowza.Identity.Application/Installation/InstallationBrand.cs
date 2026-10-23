using System.Globalization;
using Flowza.Common.Results;

namespace Flowza.Identity.Application.Installation;

/// <summary>
/// The installation's name and the initials mark shown until a logo can be uploaded (spec 007 UI-08, C6, C7; plan 007
/// §5.3). Only this class computes initials, so the login pages and the web app can never disagree.
/// </summary>
public sealed record InstallationBrand
{
    /// <summary>The name used when an installation sets none (C7).</summary>
    public const string DefaultName = "Flowza";

    /// <summary>Longest name, in characters as people see them (text elements).</summary>
    public const int MaxNameLength = 100;

    public static Error InvalidName { get; } = Error.Validation(
        "installation.name_invalid", $"The installation name must have 1 to {MaxNameLength} characters and not be blank.");

    private InstallationBrand(string name, string initials)
    {
        Name = name;
        Initials = initials;
    }

    public string Name { get; }

    public string Initials { get; }

    /// <summary>A brand from a configured name: trimmed, 1–<see cref="MaxNameLength"/> characters, not blank.</summary>
    public static Result<InstallationBrand> Create(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        var length = new StringInfo(trimmed).LengthInTextElements;
        return length is 0 or > MaxNameLength
            ? InvalidName
            : new InstallationBrand(trimmed, InitialsOf(trimmed));
    }

    /// <summary>
    /// C6: the first letters of the first two words, in capitals; a one-word name gives its first two letters.
    /// "Letters" are text elements, so a consonant with its vowel sign (Hindi, Tamil, …) stays one letter.
    /// O(length of the first two words).
    /// </summary>
    public static string InitialsOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var words = name.Split((char[]?)null, 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var initials = words.Length switch
        {
            0 => string.Empty,
            1 => FirstTextElements(words[0], 2),
            _ => FirstTextElements(words[0], 1) + FirstTextElements(words[1], 1),
        };
        return initials.ToUpperInvariant();
    }

    private static string FirstTextElements(string word, int count)
    {
        var info = new StringInfo(word);
        return info.SubstringByTextElements(0, Math.Min(count, info.LengthInTextElements));
    }
}
