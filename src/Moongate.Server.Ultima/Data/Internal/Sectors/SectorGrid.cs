namespace Moongate.Server.Ultima.Data.Internal.Sectors;

/// <summary>
///     The sectors of one map, indexed <c>sy * Columns + sx</c>; a sector exists once a mobile entered it.
/// </summary>
public sealed class SectorGrid
{
    public int Width { get; }

    public int Height { get; }

    public int Columns { get; }

    public int Rows { get; }

    public Sector?[] Cells { get; }

    public SectorGrid(int width, int height, int columns, int rows)
    {
        Width = width;
        Height = height;
        Columns = columns;
        Rows = rows;
        Cells = new Sector?[columns * rows];
    }
}
