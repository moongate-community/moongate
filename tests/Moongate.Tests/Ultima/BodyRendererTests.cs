using Moongate.Tests.Support;
using Moongate.Ultima.Animation;
using Moongate.Ultima.Io;
using Moongate.Ultima.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class BodyRendererTests
{
    [Fact]
    public void GetBodyFrames_MissingBody_ReturnsEmptyList()
    {
        var dir = CreateFixtureDirectory();

        try
        {
            Files.SetDirectory(dir);
            Animations.Reload();

            Assert.Empty(new BodyRenderer().GetBodyFrames(150, 0, 1));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetBodyFrames_ReturnsAllFramesWithAnchors()
    {
        var dir = CreateFixtureDirectory();

        try
        {
            Files.SetDirectory(dir);
            Animations.Reload();

            var frames = new BodyRenderer().GetBodyFrames(1, 0, 1);

            Assert.Equal(3, frames.Count);
            Assert.All(
                frames,
                f =>
                {
                    Assert.Equal(2, f.Width);
                    Assert.Equal(2, f.Height);
                    Assert.Equal(0, f.Png.Position);
                }
            );
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetBodyImage_FrameBeyondCount_ReturnsNull()
    {
        var dir = CreateFixtureDirectory();

        try
        {
            Files.SetDirectory(dir);
            Animations.Reload();

            Assert.Null(new BodyRenderer().GetBodyImage(1, 0, 1, 99));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetBodyImage_InvalidDirection_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BodyRenderer().GetBodyImage(1, direction: 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BodyRenderer().GetBodyImage(1, frame: -1));
    }

    [Fact]
    public void GetBodyImage_KnownBody_ReturnsDecodablePng()
    {
        var dir = CreateFixtureDirectory();

        try
        {
            Files.SetDirectory(dir);
            Animations.Reload();

            using var png = new BodyRenderer().GetBodyImage(1, 0, 1);

            Assert.NotNull(png);

            using var image = Image.Load<Bgra32>(png);
            Assert.Equal(2, image.Width);
            Assert.Equal(2, image.Height);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetBodyImage_MissingBody_ReturnsNull()
    {
        var dir = CreateFixtureDirectory();

        try
        {
            Files.SetDirectory(dir);
            Animations.Reload();

            Assert.Null(new BodyRenderer().GetBodyImage(150, 0, 1));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string CreateFixtureDirectory()
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

        return UltimaFixtures.CreateClientDirectory(("anim.idx", index), ("anim.mul", anim));
    }
}
