using Moongate.Tests.Support;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Io;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class TexmapsTests
{
    [Fact]
    public void GetRawTexmap_ReturnsSizeAndRawData()
    {
        var (idx, mul) = UltimaFixtures.BuildTexmap(0, 64, 0x1000);
        var dir = UltimaFixtures.CreateClientDirectory(("texidx.mul", idx), ("texmaps.mul", mul));

        try
        {
            Files.SetDirectory(dir);
            Texmaps.Reload();

            var raw = Texmaps.GetRawTexmap(0, out var size);

            Assert.NotNull(raw);
            Assert.Equal(64, size);
            Assert.Equal(64 * 64 * 2, raw.Length);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetTexmap_LargeFixture_Returns128Bitmap()
    {
        var (idx, mul) = UltimaFixtures.BuildTexmap(1, 128, 0x03FF);
        var dir = UltimaFixtures.CreateClientDirectory(("texidx.mul", idx), ("texmaps.mul", mul));

        try
        {
            Files.SetDirectory(dir);
            Texmaps.Reload();

            var bitmap = Texmaps.GetTexmap(1);

            Assert.NotNull(bitmap);
            Assert.Equal(128, bitmap.Width);
            Assert.Equal(128, bitmap.Height);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public unsafe void GetTexmap_SmallFixture_ReturnsOpaque64Bitmap()
    {
        var (idx, mul) = UltimaFixtures.BuildTexmap(0, 64, 0x1234);
        var dir = UltimaFixtures.CreateClientDirectory(("texidx.mul", idx), ("texmaps.mul", mul));

        try
        {
            Files.SetDirectory(dir);
            Texmaps.Reload();

            var bitmap = Texmaps.GetTexmap(0);

            Assert.NotNull(bitmap);
            Assert.Equal(64, bitmap.Width);
            Assert.Equal(64, bitmap.Height);
            Assert.Equal(0x9234, ((ushort*)bitmap.Scan0)[0]); // 0x1234 with the alpha bit forced on
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
