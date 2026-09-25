namespace Moongate.Server.Ultima.Data.Regions;

/// <summary>
///     One rectangle of a region. The start is included and the end is not; without <see cref="Z1" /> and
///     <see cref="Z2" /> the rectangle covers every height.
/// </summary>
public class RegionAreaContent
{
    public int X1 { get; set; }

    public int Y1 { get; set; }

    public int X2 { get; set; }

    public int Y2 { get; set; }

    public int? Z1 { get; set; }

    public int? Z2 { get; set; }

    /// <summary>
    ///     Returns whether the point is inside the rectangle, at a height inside its range when it has one.
    /// </summary>
    public bool Contains(int x, int y, int z)
    {
        return x >= X1 && x < X2 && y >= Y1 && y < Y2 && (Z1 is not { } low || z >= low) && (Z2 is not { } high || z < high);
    }
}
