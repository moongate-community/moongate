using Moongate.Tests.Support;
using Moongate.Ultima.Maps;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class TileMatrixTests
{
    [Fact]
    public void GetLandTile_OutOfBounds_ReturnsInvalidBlockTile()
    {
        var mapBlock = UltimaFixtures.BuildMapBlock(0x0003, 5);
        var dir = UltimaFixtures.CreateClientDirectory(("map0.mul", mapBlock));

        try
        {
            using var matrix = new TileMatrix(0, 0, 8, 8, dir);

            var outside = matrix.GetLandTile(100, 100);
            Assert.Equal(0, outside.Id);
            Assert.Equal(0, outside.Z);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetLandTile_SingleBlockMulMap_ReturnsIdAndZ()
    {
        var mapBlock = UltimaFixtures.BuildMapBlock(0x0003, 5);
        UltimaFixtures.SetMapCell(mapBlock, 2, 4, 0x00A8, -3);

        var dir = UltimaFixtures.CreateClientDirectory(("map0.mul", mapBlock));

        try
        {
            using var matrix = new TileMatrix(0, 0, 8, 8, dir);

            var plain = matrix.GetLandTile(0, 0);
            Assert.Equal(0x0003, plain.Id);
            Assert.Equal(5, plain.Z);

            var water = matrix.GetLandTile(2, 4);
            Assert.Equal(0x00A8, water.Id);
            Assert.Equal(-3, water.Z);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetLandTile_SingleBlockUopMap_ReturnsIdAndZ()
    {
        var mapBlock = UltimaFixtures.BuildMapBlock(0x0003, 5);
        UltimaFixtures.SetMapCell(mapBlock, 6, 1, 0x0245, 12);

        var uop = UltimaFixtures.BuildMapUop("map0legacymul", mapBlock);
        var dir = UltimaFixtures.CreateClientDirectory(("map0LegacyMUL.uop", uop));

        try
        {
            using var matrix = new TileMatrix(0, 0, 8, 8, dir);

            var plain = matrix.GetLandTile(0, 0);
            Assert.Equal(0x0003, plain.Id);
            Assert.Equal(5, plain.Z);

            var marked = matrix.GetLandTile(6, 1);
            Assert.Equal(0x0245, marked.Id);
            Assert.Equal(12, marked.Z);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetStaticTiles_SingleBlock_ReturnsStaticsForCell()
    {
        var mapBlock = UltimaFixtures.BuildMapBlock(0x0003, 0);
        var (index, statics) = UltimaFixtures.BuildStatics(
            (Id: 0x0ECA, CellX: 1, CellY: 2, Z: 4, Hue: 0),
            (Id: 0x0ECB, CellX: 1, CellY: 2, Z: 6, Hue: 33),
            (Id: 0x1BC3, CellX: 7, CellY: 7, Z: 0, Hue: 0)
        );

        var dir = UltimaFixtures.CreateClientDirectory(
            ("map0.mul", mapBlock),
            ("staidx0.mul", index),
            ("statics0.mul", statics)
        );

        try
        {
            using var matrix = new TileMatrix(0, 0, 8, 8, dir);

            var cell = matrix.GetStaticTiles(1, 2);
            Assert.Equal(2, cell.Length);
            Assert.Equal(0x0ECA, cell[0].Id);
            Assert.Equal(4, cell[0].Z);
            Assert.Equal(0x0ECB, cell[1].Id);
            Assert.Equal(33, cell[1].Hue);

            var corner = matrix.GetStaticTiles(7, 7);
            Assert.Single(corner);
            Assert.Equal(0x1BC3, corner[0].Id);

            var empty = matrix.GetStaticTiles(0, 0);
            Assert.Empty(empty);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
