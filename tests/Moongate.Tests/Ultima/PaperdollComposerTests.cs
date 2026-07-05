using Moongate.Tests.Support;
using Moongate.Ultima.Data;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Io;
using Moongate.Ultima.Rendering;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class PaperdollComposerTests
{
    private const int TunicItemId = 0x10;
    private const int TunicAnim = 100; // gump = 100 + 50000

    private static string CreateFixtureDirectory()
    {
        var tileData = UltimaFixtures.BuildTileData();
        UltimaFixtures.SetItem(tileData, TunicItemId, (uint)TileFlag.Wearable, 0, "tunic", TunicAnim);

        var (gumpIndex, gumps) = UltimaFixtures.BuildGumps(
            (0x07D0, 260, 237, 0x0100),           // male background, dark green
            (0x000C, 100, 150, 0x7FFF),           // male body, white/gray (huable)
            (TunicAnim + 50000, 50, 60, 0x001F)   // tunic gump, blue
        );

        var hues = UltimaFixtures.BuildHues("Skin", 0x7C00, 0, 0);

        return UltimaFixtures.CreateClientDirectory(
            ("tiledata.mul", tileData),
            ("gumpidx.mul", gumpIndex),
            ("gumpart.mul", gumps),
            ("hues.mul", hues)
        );
    }

    private static void InitializeReaders()
    {
        Gumps.Reload();
        TileData.Initialize();
        Hues.Initialize();
    }

    [Fact]
    public void Compose_ReturnsCanvasSizedPng()
    {
        var dir = CreateFixtureDirectory();

        try
        {
            Files.SetDirectory(dir);
            InitializeReaders();

            using var png = new PaperdollComposer().Compose(new PaperdollRequest());

            Assert.NotNull(png);

            using var image = Image.Load<Bgra32>(png);
            Assert.Equal(260, image.Width);
            Assert.Equal(237, image.Height);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Compose_EquipmentCoversBody()
    {
        var dir = CreateFixtureDirectory();

        try
        {
            Files.SetDirectory(dir);
            InitializeReaders();

            var request = new PaperdollRequest
            {
                Equipment = [new PaperdollEquipEntry { ItemId = TunicItemId }]
            };

            using var png = new PaperdollComposer().Compose(request);

            Assert.NotNull(png);

            using var image = Image.Load<Bgra32>(png);

            // tunic gump is blue (0x001F -> pure blue) and drawn above the body at (0,0)
            Assert.Equal((byte)255, image[0, 0].B);
            Assert.Equal((byte)0, image[0, 0].R);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Compose_WithoutBackground_LeavesUncoveredPixelsTransparent()
    {
        var dir = CreateFixtureDirectory();

        try
        {
            Files.SetDirectory(dir);
            InitializeReaders();

            using var png = new PaperdollComposer().Compose(new PaperdollRequest { IncludeBackground = false });

            Assert.NotNull(png);

            using var image = Image.Load<Bgra32>(png);
            Assert.Equal((byte)0, image[259, 236].A); // bottom-right: no body/equipment there
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Compose_NullRequest_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PaperdollComposer().Compose(null!));
    }

    [Fact]
    public void Compose_MissingBodyGump_ReturnsNull()
    {
        // directory with background only: body gump 0x000C absent -> composition fails cleanly
        var (gumpIndex, gumps) = UltimaFixtures.BuildGumps((0x07D0, 260, 237, 0x0100));
        var dir = UltimaFixtures.CreateClientDirectory(
            ("tiledata.mul", UltimaFixtures.BuildTileData()),
            ("gumpidx.mul", gumpIndex),
            ("gumpart.mul", gumps),
            ("hues.mul", UltimaFixtures.BuildHues("h", 1, 0, 0))
        );

        try
        {
            Files.SetDirectory(dir);
            InitializeReaders();

            Assert.Null(new PaperdollComposer().Compose(new PaperdollRequest()));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
