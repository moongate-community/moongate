using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Teleporters;

/// <summary>A map + world coordinate endpoint of a teleporter (source or destination).</summary>
public sealed class TeleporterEndpoint
{
    public MapType Map { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
}
