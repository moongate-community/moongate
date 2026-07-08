using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Signs;

/// <summary>
/// A sign placed in the world: its map, coordinates, item id and label. The label is either a
/// cliloc reference (e.g. <c>#1016093</c>) or literal text.
/// </summary>
public sealed class SignEntry
{
    public MapType Map { get; set; }
    public int ItemId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public string Label { get; set; } = string.Empty;
}
