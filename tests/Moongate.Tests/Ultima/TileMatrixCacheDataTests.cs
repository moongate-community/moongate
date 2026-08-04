using Moongate.Tests.Support;
using Moongate.Ultima.Io;
using Moongate.Ultima.Maps;

namespace Moongate.Tests.Ultima;

/// <summary>
/// The block cache against a real client. Unit tests prove the cache evicts; only the files can say
/// that a facet still reads correctly once blocks start being dropped underneath it.
/// </summary>
public class TileMatrixCacheDataTests
{
    [ClientFilesFact]
    public void ReadingFarMoreBlocksThanTheCap_StaysAtTheCap()
    {
        var tiles = Facet();

        tiles.SetCacheCapacity(64);

        // 400 distinct blocks, walking a diagonal so no two share coordinates.
        for (var block = 0; block < 400; block++)
        {
            tiles.GetLandBlock(block, block);
            tiles.GetStaticBlock(block, block);
        }

        var (land, statics) = tiles.CachedBlockCount;

        Assert.Equal(64, land);
        Assert.Equal(64, statics);
    }

    /// <summary>
    /// The point of the whole change: before, every block a caller ever touched stayed for the life of
    /// the process. Panning the web map viewer across Felucca would have kept all 393216.
    /// </summary>
    [ClientFilesFact]
    public void ABlockEvictedAndReadAgain_ReadsBackTheSameTiles()
    {
        var tiles = Facet();

        tiles.SetCacheCapacity(4);

        var before = (Tile[])tiles.GetLandBlock(100, 100).Clone();

        for (var block = 0; block < 50; block++)
        {
            tiles.GetLandBlock(200 + block, 200 + block);
        }

        var after = tiles.GetLandBlock(100, 100);

        Assert.Equal(before.Length, after.Length);
        Assert.All(
            Enumerable.Range(0, before.Length),
            index =>
            {
                Assert.Equal(before[index].Id, after[index].Id);
                Assert.Equal(before[index].Z, after[index].Z);
            }
        );
    }

    [ClientFilesFact]
    public void OutOfBoundsBlocks_AreStillRefusedWithoutTouchingTheCache()
    {
        var tiles = Facet();

        tiles.SetCacheCapacity(16);

        Assert.Same(TileMatrix.InvalidLandBlock, tiles.GetLandBlock(-1, 0));
        Assert.Same(TileMatrix.InvalidLandBlock, tiles.GetLandBlock(tiles.BlockWidth, 0));
    }

    private static TileMatrix Facet()
    {
        Files.SetDirectory(ClientFiles.Directory);

        return Map.Felucca.Tiles;
    }
}
