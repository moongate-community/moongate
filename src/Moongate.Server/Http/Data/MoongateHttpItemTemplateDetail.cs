using Moongate.UO.Data.Templates.Items;

namespace Moongate.Server.Http.Data;

/// <summary>
/// Detailed item-template payload returned by HTTP detail endpoints.
/// </summary>
public sealed class MoongateHttpItemTemplateDetail
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Category { get; init; }

    public required string ItemId { get; init; }

    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string ScriptId { get; init; } = string.Empty;

    public decimal? Weight { get; init; }

    public string? GoldValue { get; init; }

    public string? Hue { get; init; }

    public string? GumpId { get; init; }

    public string Rarity { get; init; } = string.Empty;

    public IReadOnlyList<string> Container { get; init; } = [];

    public IReadOnlyList<MoongateHttpItemTemplateContainerItem> ContainerItems { get; init; } = [];

    public required IReadOnlyDictionary<string, ItemTemplateParamDefinition> Params { get; init; }
}
