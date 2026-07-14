using Moongate.Tests.Support;
using Moongate.Ultima.Animation;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Io;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class SyntheticAssetFixtureTests
{
    [Fact]
    public unsafe void BuildAnim_IsDecodedByAnimationsReader()
    {
        var (index, anim) = UltimaFixtures.BuildAnim(
            1,
            0,
            1,
            3,
            2,
            2,
            1,
            0x7C1F
        );
        var dir = UltimaFixtures.CreateClientDirectory(("anim.idx", index), ("anim.mul", anim));

        try
        {
            Files.SetDirectory(dir);
            Animations.Reload();

            var frames = Animations.GetAnimation(1, 0, 1, 1);

            Assert.NotNull(frames);
            Assert.Equal(3, frames.Length);
            Assert.NotNull(frames[0].Bitmap);
            Assert.Equal(2, frames[0].Bitmap.Width);
            Assert.Equal(2, frames[0].Bitmap.Height);
            Assert.Equal(0x7C1F, ((ushort*)frames[0].Bitmap.Scan0)[0] & 0x7FFF);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public unsafe void BuildGumps_AreDecodedByGumpReader()
    {
        var (index, gumps) = UltimaFixtures.BuildGumps((0x07D0, 3, 2, 0x7C00), (0x000C, 2, 2, 0x001F));
        var dir = UltimaFixtures.CreateClientDirectory(("gumpidx.mul", index), ("gumpart.mul", gumps));

        try
        {
            Files.SetDirectory(dir);
            Gumps.Reload();

            using var background = Gumps.GetGump(0x07D0);
            using var body = Gumps.GetGump(0x000C);

            Assert.NotNull(background);
            Assert.Equal(3, background.Width);
            Assert.Equal(2, background.Height);
            Assert.Equal(0x7C00, ((ushort*)background.Scan0)[0] & 0x7FFF);

            Assert.NotNull(body);
            Assert.Equal(0x001F, ((ushort*)body.Scan0)[0] & 0x7FFF);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public unsafe void BuildStaticArt_IsDecodedByArtReader()
    {
        var (index, art) = UltimaFixtures.BuildStaticArt(0x10, 2, 2, 0xFC00);
        var dir = UltimaFixtures.CreateClientDirectory(("artidx.mul", index), ("art.mul", art));

        try
        {
            Files.SetDirectory(dir);
            Art.Reload();

            using var bmp = Art.GetStatic(0x10);

            Assert.NotNull(bmp);
            Assert.Equal(2, bmp.Width);
            Assert.Equal(2, bmp.Height);
            Assert.Equal(0xFC00, ((ushort*)bmp.Scan0)[0]);
            Assert.Equal(0xFC00, ((ushort*)bmp.Scan0)[3]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
