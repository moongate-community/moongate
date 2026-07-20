using System.Globalization;

namespace Moongate.Http.Plugin.Data;

/// <summary>
/// The paging and search arguments every listing route accepts, parsed and validated in one place so each
/// route does not invent its own rules.
/// </summary>
public sealed class PageRequest
{
    /// <summary>What a caller gets without asking. Big enough to be useful, small enough to be cheap.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// The ceiling on one page. Asking for more is an error rather than a capped page: a caller that
    /// requested 5000 and silently received 100 believes it has read everything.
    /// </summary>
    public const int MaxPageSize = 100;

    private PageRequest(int page, int pageSize, string? search)
    {
        Page = page;
        PageSize = pageSize;
        Search = search;
    }

    /// <summary>1-based, as it appears on the wire.</summary>
    public int Page { get; }

    public int PageSize { get; }

    /// <summary>The trimmed search text, or null when there is nothing to filter on.</summary>
    public string? Search { get; }

    /// <summary>
    /// The 0-based offset for the store. The one place the wire's 1-based page becomes an offset: doing it
    /// per route is how a page quietly repeats or drops a row.
    /// </summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Parses the raw query values. Returns false with a caller-facing reason rather than correcting
    /// anything: an out-of-range page or size is a bug in the caller, and clamping it hides that bug behind
    /// a plausible-looking response.
    /// </summary>
    public static bool TryParse(
        string? page,
        string? pageSize,
        string? search,
        out PageRequest request,
        out string? error
    )
    {
        request = null!;
        error = null;

        var pageNumber = 1;

        if (!string.IsNullOrWhiteSpace(page) &&
            (!int.TryParse(page, NumberStyles.Integer, CultureInfo.InvariantCulture, out pageNumber) || pageNumber < 1))
        {
            error = $"'{page}' is not a page. Expected a whole number from 1 up.";

            return false;
        }

        var size = DefaultPageSize;

        if (!string.IsNullOrWhiteSpace(pageSize) &&
            (!int.TryParse(pageSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out size) ||
             size < 1 ||
             size > MaxPageSize))
        {
            error = $"'{pageSize}' is not a page size. Expected a whole number from 1 to {MaxPageSize}.";

            return false;
        }

        // Blank and absent must mean the same thing, or "?search=" filters differently from no search.
        var trimmed = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        request = new(pageNumber, size, trimmed);

        return true;
    }
}
