using Moongate.Tests.Support;
using Moongate.Ultima;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class TileDataTests
{
    [Fact]
    public void Initialize_OldFormatFixture_ParsesLandAndItemTables()
    {
        byte[] tileData = UltimaFixtures.BuildTileData();
        UltimaFixtures.SetLand(tileData, 3, (uint)TileFlag.Impassable, 0x001A, "grass");
        UltimaFixtures.SetItem(tileData, 5, (uint)(TileFlag.Wet | TileFlag.Surface), 7, "water barrel");

        string dir = UltimaFixtures.CreateClientDirectory(("tiledata.mul", tileData));

        try
        {
            Files.SetDirectory(dir);
            TileData.Initialize();

            Assert.Equal(0x4000, TileData.LandTable.Length);
            Assert.Equal(32, TileData.ItemTable.Length);

            Assert.Equal("grass", TileData.LandTable[3].Name);
            Assert.Equal(0x001A, TileData.LandTable[3].TextureId);
            Assert.True((TileData.LandTable[3].Flags & TileFlag.Impassable) != 0);

            Assert.Equal("water barrel", TileData.ItemTable[5].Name);
            Assert.Equal(7, TileData.ItemTable[5].Height);
            Assert.True((TileData.ItemTable[5].Flags & TileFlag.Wet) != 0);
            Assert.True((TileData.ItemTable[5].Flags & TileFlag.Surface) != 0);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Initialize_UnsetEntries_HaveEmptyNameAndNoFlags()
    {
        byte[] tileData = UltimaFixtures.BuildTileData();
        string dir = UltimaFixtures.CreateClientDirectory(("tiledata.mul", tileData));

        try
        {
            Files.SetDirectory(dir);
            TileData.Initialize();

            Assert.Equal(string.Empty, TileData.LandTable[100].Name);
            Assert.Equal(TileFlag.None, TileData.LandTable[100].Flags);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
