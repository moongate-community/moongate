using Moongate.Http.Plugin.Data;

namespace Moongate.Tests.Http;

public class MapTileGeometryTests
{
    [Fact]
    public void TileSize_IsAWholeNumberOfBlocks()
    {
        // The reason 256 was chosen: a native tile is exactly 32 blocks, so GetImage never gets a
        // fractional block and tiles never straddle one.
        Assert.Equal(0, MapTileGeometry.TileSize % 8);
        Assert.Equal(MapTileGeometry.TileSize / 8, MapTileGeometry.BlocksPerTile);
    }

    [Theory]
    [InlineData(6144, 4096, 5)] // Felucca and Trammel: 6144/256 = 24, ceil(log2 24) = 5
    [InlineData(2304, 1600, 4)] // Ilshenar: 2304/256 = 9, ceil(log2 9) = 4
    [InlineData(2560, 2048, 4)] // Malas: 2560/256 = 10, ceil(log2 10) = 4
    [InlineData(1448, 1448, 3)] // Tokuno: 1448/256 = 5.65, ceil(log2 5.65) = 3
    [InlineData(1280, 4096, 4)] // TerMur: tall, so height drives it: 4096/256 = 16, log2 16 = 4
    [InlineData(256, 256, 0)]   // one tile already: no pyramid
    public void MaxZoom_IsTheSmallestZoomWholeMapFitsOneTileAtZero(int width, int height, int expected)
    {
        Assert.Equal(expected, MapTileGeometry.MaxZoom(width, height));
    }

    [Theory]
    [InlineData(6144, 4096)]
    [InlineData(2304, 1600)]
    [InlineData(1448, 1448)]
    [InlineData(1280, 4096)]
    public void MaxZoom_AlwaysLeavesExactlyOneTileAtZoomZero(int width, int height)
    {
        // The property MaxZoom exists to guarantee. If z0 were ever a 2x1 grid, a viewer would open on
        // half a map and nothing in the response would say so.
        var maxZoom = MapTileGeometry.MaxZoom(width, height);

        Assert.Equal(1, MapTileGeometry.TilesAcross(width, 0, maxZoom));
        Assert.Equal(1, MapTileGeometry.TilesDown(height, 0, maxZoom));
    }

    [Fact]
    public void Scale_IsOneAtNativeAndDoublesDownwards()
    {
        Assert.Equal(1, MapTileGeometry.Scale(5, 5));
        Assert.Equal(2, MapTileGeometry.Scale(4, 5));
        Assert.Equal(32, MapTileGeometry.Scale(0, 5));
    }

    [Fact]
    public void TilesAcross_AtNative_IsTheMapRoundedUpToWholeTiles()
    {
        Assert.Equal(24, MapTileGeometry.TilesAcross(6144, 5, 5));
        Assert.Equal(16, MapTileGeometry.TilesDown(4096, 5, 5));
    }

    [Fact]
    public void TilesAcross_RoundsUpSoTheLastPartialTileStillExists()
    {
        // 1448 is 5.65 tiles across at native. Rounding down would drop the right edge of Tokuno.
        Assert.Equal(6, MapTileGeometry.TilesAcross(1448, 3, 3));
    }

    [Fact]
    public void Grids_DoNotAlwaysHalveCleanly()
    {
        // Felucca's z1 is 2x1, so its single z0 tile asks for children (0,1) and (1,1), which do not
        // exist. This is the case that makes a "fetch all four children" composer crash on every facet.
        var maxZoom = MapTileGeometry.MaxZoom(6144, 4096);

        Assert.Equal(2, MapTileGeometry.TilesAcross(6144, 1, maxZoom));
        Assert.Equal(1, MapTileGeometry.TilesDown(4096, 1, maxZoom));
    }
}
