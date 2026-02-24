using Moongate.UO.Data.Utils;

namespace Moongate.Tests.UO.Data.Maps;

public class MapSectorConstsTests
{
    [Test]
    public void SectorShift_ShouldMatchSectorSizePowerOfTwo()
    {
        var computedSize = 1 << MapSectorConsts.SectorShift;

        Assert.That(computedSize, Is.EqualTo(MapSectorConsts.SectorSize));
    }
}
