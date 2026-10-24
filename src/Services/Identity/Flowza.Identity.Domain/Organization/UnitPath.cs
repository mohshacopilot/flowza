using System.Globalization;
using Flowza.Common.Results;

namespace Flowza.Identity.Domain.Organization;

/// <summary>
/// Where a unit sits in the tree: the unit numbers from the root down to the unit, e.g. <c>/1/4/9/</c> (plan 002 §8,
/// materialized path). "Everything below a unit" is every path that starts with the unit's path (one index range seek);
/// "everything above" is read from the path itself, O(depth). The numbers are small internal keys, never shown.
/// </summary>
public sealed record UnitPath
{
    /// <summary>Levels a tree may have, the root included (PA4); far beyond real organizations (4–8 levels).</summary>
    public const int MaxDepth = 50;

    /// <summary>Column size (PA4): 50 levels of 10-digit numbers plus separators fit easily.</summary>
    public const int MaxLength = 900;

    private const char Separator = '/';

    private UnitPath(string value) => Value = value;

    public string Value { get; }

    /// <summary>Levels from the root to this unit; the root has depth 1.</summary>
    public int Depth => Value.Count(c => c == Separator) - 1;

    /// <summary>The unit numbers from the root down to this unit (the last one is the unit's own). O(depth).</summary>
    public IReadOnlyList<int> Numbers =>
        [.. Value.Split(Separator, StringSplitOptions.RemoveEmptyEntries).Select(n => int.Parse(n, CultureInfo.InvariantCulture))];

    public static UnitPath ForRoot(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        return new UnitPath(Format(string.Empty, number));
    }

    /// <summary>Rebuilds a value that was valid when it was stored (persistence only).</summary>
    public static UnitPath FromStored(string value) => new(value);

    /// <summary>The path of a new child with this number; refused below <see cref="MaxDepth"/> levels.</summary>
    public Result<UnitPath> Append(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        return Depth >= MaxDepth ? OrgErrors.TreeTooDeep : new UnitPath(Format(Value, number));
    }

    /// <summary>
    /// Whether this path is <paramref name="ancestor"/> itself or lies below it. O(1) apart from the prefix comparison
    /// (at most <see cref="MaxLength"/> characters). The trailing separator keeps <c>/1/4/</c> apart from <c>/1/42/</c>.
    /// </summary>
    public bool IsSameOrBelow(UnitPath ancestor)
    {
        ArgumentNullException.ThrowIfNull(ancestor);
        return Value.StartsWith(ancestor.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// This path after its ancestor <paramref name="from"/> moved to <paramref name="to"/>: the prefix is replaced, the
    /// rest stays. Refused when the result would be deeper than <see cref="MaxDepth"/>.
    /// </summary>
    public Result<UnitPath> Rebase(UnitPath from, UnitPath to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (!IsSameOrBelow(from))
        {
            throw new ArgumentException("The path does not lie below the moved unit.", nameof(from));
        }

        var moved = new UnitPath(to.Value + Value[from.Value.Length..]);
        return moved.Depth > MaxDepth ? OrgErrors.TreeTooDeep : moved;
    }

    public override string ToString() => Value;

    private static string Format(string parent, int number) =>
        (parent.Length == 0 ? "/" : parent) + number.ToString(CultureInfo.InvariantCulture) + Separator;
}
