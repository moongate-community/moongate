namespace Moongate.Http.Plugin.Data;

/// <summary>One page of results, and what a caller needs to walk the rest.</summary>
/// <param name="Items">The results on this page.</param>
/// <param name="Total">How many results matched in total, before paging.</param>
/// <param name="Page">The 1-based page this is.</param>
/// <param name="PageSize">The page size used. The last page may hold fewer items.</param>
/// <param name="TotalPages">How many pages exist at this page size. 0 when nothing matched.</param>
public record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize, int TotalPages)
{
    /// <summary>
    /// Wraps a page in the request that asked for it. TotalPages rounds up: 51 results at 25 a page is
    /// three pages, and rounding down would hide the last one from any caller that trusts the count.
    /// </summary>
    public static PagedResponse<T> From(IReadOnlyList<T> items, int total, PageRequest request)
        => new(
            items,
            total,
            request.Page,
            request.PageSize,
            (int)Math.Ceiling(total / (double)request.PageSize)
        );
}
