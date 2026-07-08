namespace Moongate.UO.Data.Regions;

/// <summary>An axis-aligned rectangle (inclusive corners) covered by a region.</summary>
public sealed class RegionRectangle
{
    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }
}
