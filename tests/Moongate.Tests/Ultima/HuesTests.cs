using Moongate.Tests.Support;
using Moongate.Ultima;

using Moongate.Ultima.Io;

using Moongate.Ultima.Graphics;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class HuesTests
{
    [Fact]
    public void Initialize_SingleBlockFixture_ParsesFirstHue()
    {
        byte[] hues = UltimaFixtures.BuildHues("Test Hue", 0x1234, tableStart: 2, tableEnd: 9);
        string dir = UltimaFixtures.CreateClientDirectory(("hues.mul", hues));

        try
        {
            Files.SetDirectory(dir);
            Hues.Initialize();

            Assert.Equal(3000, Hues.List.Length);

            Hue first = Hues.GetHue(0);
            Assert.Equal("Test Hue", first.Name);
            Assert.Equal(0x1234, first.Colors[0]);
            Assert.Equal(2, first.TableStart);
            Assert.Equal(9, first.TableEnd);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Initialize_ZeroColors_AreClampedToOne()
    {
        byte[] hues = UltimaFixtures.BuildHues("Clamped", 0x0000, 0, 0);
        string dir = UltimaFixtures.CreateClientDirectory(("hues.mul", hues));

        try
        {
            Files.SetDirectory(dir);
            Hues.Initialize();

            // 0 is a flag-like invalid color on disk; the loader clamps it to 1.
            Assert.Equal(1, Hues.GetHue(0).Colors[0]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetHue_IndexIsMasked_OutOfRangeFallsBackToHueZero()
    {
        byte[] hues = UltimaFixtures.BuildHues("Fallback", 0x7FFF, 0, 0);
        string dir = UltimaFixtures.CreateClientDirectory(("hues.mul", hues));

        try
        {
            Files.SetDirectory(dir);
            Hues.Initialize();

            Assert.Same(Hues.GetHue(0), Hues.GetHue(0x3500));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
