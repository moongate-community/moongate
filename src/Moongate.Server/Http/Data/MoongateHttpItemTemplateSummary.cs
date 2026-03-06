using Moongate.UO.Data.Templates.Items;

namespace Moongate.Server.Http.Data;

/// <summary>
/// Lightweight item-template payload for paged listings.
/// </summary>
public sealed class MoongateHttpItemTemplateSummary
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Category { get; init; }

    public required string ItemId { get; init; }

    public required IReadOnlyDictionary<string, ItemTemplateParamDefinition> Params { get; init; }
}
