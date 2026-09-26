using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Tiles;

/// <summary>
///     One item graphic from <c>tiledata.mul</c>, such as a wall, a door or a backpack.
/// </summary>
public sealed class ItemTile
{
    /// <summary>
    ///     Gets the item graphic id.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    ///     Gets the name the client files give the item, such as <c>backpack</c>.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the tile flags, such as <see cref="TileFlagType.Impassable" />, <see cref="TileFlagType.Container" /> or
    ///     <see cref="TileFlagType.Wearable" />.
    /// </summary>
    public TileFlagType Flags { get; init; }

    /// <summary>
    ///     Gets the default weight in stones; 255 means the item cannot be picked up.
    /// </summary>
    public byte Weight { get; init; }

    /// <summary>
    ///     Gets the height in Z units, the space the item takes up.
    /// </summary>
    public byte Height { get; init; }

    /// <summary>
    ///     Gets the height a mobile stands on: half the <see cref="Height" /> for a <see cref="TileFlagType.Bridge" />,
    ///     such as a stair, the full height otherwise.
    /// </summary>
    public int StandHeight { get; init; }

    /// <summary>
    ///     Gets the equipment layer of a wearable item; the client files call this field quality.
    /// </summary>
    public byte Layer { get; init; }

    /// <summary>
    ///     Gets the default amount of a stackable item, or the light id of a light source.
    /// </summary>
    public byte Quantity { get; init; }

    /// <summary>
    ///     Gets the body animation shown when a mobile wears the item.
    /// </summary>
    public short Animation { get; init; }
}
