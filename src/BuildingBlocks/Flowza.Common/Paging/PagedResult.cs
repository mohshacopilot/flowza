namespace Flowza.Common.Paging;

/// <summary>One page of items plus the total count, so the web app can show page links.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int Size, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)Size);
}

/// <summary>Factory for <see cref="PagedResult{T}"/>.</summary>
public static class PagedResult
{
    public static PagedResult<T> Create<T>(IReadOnlyList<T> items, PageRequest request, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(request);
        return new PagedResult<T>(items, request.Page, request.Size, totalCount);
    }
}
