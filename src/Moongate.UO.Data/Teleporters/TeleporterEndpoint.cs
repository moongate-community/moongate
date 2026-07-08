namespace Moongate.UO.Data.Teleporters;

/// <summary>A map + world coordinate endpoint of a teleporter (source or destination).</summary>
public sealed class TeleporterEndpoint
{
    public string Map { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
}
