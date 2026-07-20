namespace Moongate.Http.Plugin.Data;

/// <summary>
/// The arithmetic of the map tile pyramid. Pure: it reads no files and touches no Ultima state, which is
/// why it can be proven on its own rather than inferred from a rendered image.
/// </summary>
public static class MapTileGeometry
{
    /// <summary>
    /// 256 pixels a side. Chosen because it is exactly <see cref="BlocksPerTile" /> map blocks: a native
    /// tile is a whole number of blocks, so GetImage never gets a fraction and tiles never straddle one.
    /// </summary>
    public const int TileSize = 256;

    /// <summary>Map blocks in one native tile. A block is 8×8 tiles and renders 8×8 pixels.</summary>
    public const int BlocksPerTile = TileSize / 8;

    /// <summary>
    /// The zoom at which one pixel is one map tile — and therefore the number of halvings needed for the
    /// whole facet to fit a single tile at zoom 0.
    /// </summary>
    public static int MaxZoom(int width, int height)
    {
        var longest = Math.Max(width, height);

        if (longest <= TileSize)
        {
            return 0;
        }

        return (int)Math.Ceiling(Math.Log2(longest / (double)TileSize));
    }

    /// <summary>Map tiles per pixel at this zoom. 1 at native, doubling with every step down.</summary>
    public static int Scale(int zoom, int maxZoom)
        => 1 << (maxZoom - zoom);

    /// <summary>
    /// Tiles across the grid at this zoom. Rounded up: the last tile is usually partial, and rounding down
    /// would drop the map's right edge with nothing saying so.
    /// </summary>
    public static int TilesAcross(int width, int zoom, int maxZoom)
        => Count(width, zoom, maxZoom);

    /// <summary>Tiles down the grid at this zoom. Rounded up, for the same reason as across.</summary>
    public static int TilesDown(int height, int zoom, int maxZoom)
        => Count(height, zoom, maxZoom);

    private static int Count(int extent, int zoom, int maxZoom)
        => (int)Math.Ceiling(extent / (double)(TileSize * Scale(zoom, maxZoom)));
}
