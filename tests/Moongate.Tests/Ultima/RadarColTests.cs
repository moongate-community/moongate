using Moongate.Tests.Support;
using Moongate.Ultima.Io;
using Moongate.Ultima.Maps;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class RadarColTests
{
    [Fact]
    public void GetLandColor_OutOfRange_ReturnsZero()
    {
        var dir = UltimaFixtures.CreateClientDirectory(("radarcol.mul", UltimaFixtures.BuildRadarCol([0x1111])));

        try
        {
            Files.SetDirectory(dir);
            RadarCol.Initialize();

            Assert.Equal(0, RadarCol.GetLandColor(9999));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Initialize_RadarColFixture_ExposesLandAndItemColors()
    {
        var colors = new ushort[0x4000 + 1];
        colors[0] = 0x1234;      // land 0
        colors[1] = 0x5678;      // land 1
        colors[0x4000] = 0x0ABC; // item 0 (item colors start at +0x4000)

        var dir = UltimaFixtures.CreateClientDirectory(("radarcol.mul", UltimaFixtures.BuildRadarCol(colors)));

        try
        {
            Files.SetDirectory(dir);
            RadarCol.Initialize();

            Assert.Equal(0x1234, RadarCol.GetLandColor(0));
            Assert.Equal(0x5678, RadarCol.GetLandColor(1));
            Assert.Equal(0x0ABC, RadarCol.GetItemColor(0));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
