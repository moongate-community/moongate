using Moongate.Tests.Support;
using Moongate.Ultima.Catalog;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Io;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class ItemCatalogTests
{
    private const int ItemId = 0x10;

    private static string CreateFixtureDirectory()
    {
        var tileData = UltimaFixtures.BuildTileData();
        UltimaFixtures.SetItem(tileData, ItemId, (uint)(TileFlag.Wearable | TileFlag.PartialHue), 4, "test tunic");

        var (artIndex, art) = UltimaFixtures.BuildStaticArt(ItemId, 2, 2, 0xFFFF); // white -> gray, huable
        var hues = UltimaFixtures.BuildHues("Red", 0x7C00, 0, 0);

        return UltimaFixtures.CreateClientDirectory(
            ("tiledata.mul", tileData),
            ("artidx.mul", artIndex),
            ("art.mul", art),
            ("hues.mul", hues)
        );
    }

    private static void InitializeReaders()
    {
        Art.Reload();
        TileData.Initialize();
        Hues.Initialize();
    }

    [Fact]
    public void GetItem_KnownId_ReturnsEnrichedInfo()
    {
        var dir = CreateFixtureDirectory();

        try
        {
            Files.SetDirectory(dir);
            InitializeReaders();

            var catalog = new ItemCatalog();
            var info = catalog.GetItem(ItemId);

            Assert.NotNull(info);
            Assert.Equal((uint)ItemId, info.ItemId);
            Assert.Equal("test tunic", info.Name);
            Assert.Equal(4, info.Height);
            Assert.True((info.Flags & TileFlag.Wearable) != 0);
            Assert.True(info.HasArt);
            Assert.Equal(2, info.ArtWidth);
            Assert.Equal(2, info.ArtHeight);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetItem_OutOfRange_ReturnsNull()
    {
        var dir = CreateFixtureDirectory();

        try
        {
            Files.SetDirectory(dir);
            InitializeReaders();

            Assert.Null(new ItemCatalog().GetItem(uint.MaxValue));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetItemImage_ReturnsDecodablePngAtPositionZero()
    {
        var dir = CreateFixtureDirectory();

        try
        {
            Files.SetDirectory(dir);
            InitializeReaders();

            using var png = new ItemCatalog().GetItemImage(ItemId);

            Assert.NotNull(png);
            Assert.Equal(0, png.Position);

            using var image = Image.Load<Bgra32>(png);
            Assert.Equal(2, image.Width);
            Assert.Equal(2, image.Height);
            Assert.Equal((byte)255, image[0, 0].A);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetItemImage_WithHue_ChangesPixels()
    {
        var dir = CreateFixtureDirectory();

        try
        {
            Files.SetDirectory(dir);
            InitializeReaders();

            var catalog = new ItemCatalog();

            using var plain = catalog.GetItemImage(ItemId);
            using var hued = catalog.GetItemImage(ItemId, 1);

            Assert.NotNull(plain);
            Assert.NotNull(hued);

            using var plainImage = Image.Load<Bgra32>(plain);
            using var huedImage = Image.Load<Bgra32>(hued);

            Assert.NotEqual(plainImage[0, 0], huedImage[0, 0]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetItemImage_MissingArt_ReturnsNull()
    {
        var dir = CreateFixtureDirectory();

        try
        {
            Files.SetDirectory(dir);
            InitializeReaders();

            Assert.Null(new ItemCatalog().GetItemImage(0x20)); // no art at this id
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
