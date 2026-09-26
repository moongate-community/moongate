namespace Moongate.Server.Ultima.Data.Maps;

/// <summary>
///     One static object of a map cell, such as a wall, a tree or a floor tile, as the client files place it.
/// </summary>
public readonly struct MapStaticTile
{
    /// <summary>
    ///     Gets the item graphic id, the index into the item tiles of <c>ITileDataService</c>.
    /// </summary>
    public ushort Id { get; init; }

    /// <summary>
    ///     Gets the height the object stands at.
    /// </summary>
    public sbyte Z { get; init; }

    /// <summary>
    ///     Gets the hue the object is drawn with; 0 means its own colours.
    /// </summary>
    public int Hue { get; init; }
}
