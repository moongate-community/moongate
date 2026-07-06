using Moongate.Tests.Support;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Io;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class LightTests
{
    [Fact]
    public void GetLight_LightFixture_ReturnsBitmapOfExpectedSize()
    {
        var (idx, mul) = UltimaFixtures.BuildLight(0, 4, 3, 0);
        var dir = UltimaFixtures.CreateClientDirectory(("lightidx.mul", idx), ("light.mul", mul));

        try
        {
            Files.SetDirectory(dir);
            Light.Reload();

            var bitmap = Light.GetLight(0);

            Assert.NotNull(bitmap);
            Assert.Equal(4, bitmap.Width);
            Assert.Equal(3, bitmap.Height);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetRawLight_ReturnsWidthHeightAndData()
    {
        var (idx, mul) = UltimaFixtures.BuildLight(0, 4, 3, 7);
        var dir = UltimaFixtures.CreateClientDirectory(("lightidx.mul", idx), ("light.mul", mul));

        try
        {
            Files.SetDirectory(dir);
            Light.Reload();

            var raw = Light.GetRawLight(0, out var width, out var height);

            Assert.NotNull(raw);
            Assert.Equal(4, width);
            Assert.Equal(3, height);
            Assert.Equal(12, raw.Length);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
