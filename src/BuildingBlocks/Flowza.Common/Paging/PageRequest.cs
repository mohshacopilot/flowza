namespace Flowza.Common.Paging;

/// <summary>
/// A page of a list: page number (from 1) and page size (default 25, at most 100). Out-of-range values are
/// brought into range instead of failing (tech discussion T2, S7; INB Z1).
/// </summary>
public sealed record PageRequest
{
    public const int DefaultSize = 25;
    public const int MaxSize = 100;

    public PageRequest(int page = 1, int size = DefaultSize)
    {
        Page = Math.Max(1, page);
        Size = Math.Clamp(size, 1, MaxSize);
    }

    public int Page { get; }

    public int Size { get; }

    /// <summary>Number of items before this page.</summary>
    public int Skip => (Page - 1) * Size;
}
