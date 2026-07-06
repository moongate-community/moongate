using Moongate.Ultima.Types;

namespace Moongate.Ultima.Data;

/// <summary>
/// Enriched item information: the tiledata record plus art facts, for tools and UIs.
/// </summary>
public sealed record UoItemInfo
{
    public uint ItemId { get; init; }

    public string Name { get; init; } = string.Empty;

    public TileFlagType Flags { get; init; }

    public byte Weight { get; init; }

    public byte Quality { get; init; }

    public byte Quantity { get; init; }

    public byte Value { get; init; }

    public byte Hue { get; init; }

    public byte StackingOffset { get; init; }

    public byte Height { get; init; }

    public short MiscData { get; init; }

    public short Animation { get; init; }

    public LayerType Layer { get; init; }

    public bool HasArt { get; init; }

    public int ArtWidth { get; init; }

    public int ArtHeight { get; init; }
}
