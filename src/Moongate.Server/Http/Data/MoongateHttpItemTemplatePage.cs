namespace Moongate.Server.Http.Data;

/// <summary>
/// Paged response for item-template listings.
/// </summary>
public sealed class MoongateHttpItemTemplatePage
{
    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }

    public required IReadOnlyList<MoongateHttpItemTemplateSummary> Items { get; init; }
}
