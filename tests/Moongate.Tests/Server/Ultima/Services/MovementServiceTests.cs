using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Tests.TestSupport.Ultima.Maps;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class MovementServiceTests
{
    private readonly FakeMapService _map = new(16, 16);
    private readonly FakeTileDataService _tiles = new();

    [Fact]
    public void GetAverageZ_FlatLand_ReturnsItsZ()
    {
        _map.SetLandZ(0, 0, 15, 15, 5);

        Assert.Equal(5, CreateService().GetAverageZ(MapType.Felucca, 4, 4));
    }

    [Fact]
    public void GetAverageZ_TopCornerRaised_AveragesTheOtherDiagonal()
    {
        // zTop (4,4) = 10, zLeft (4,5) = zRight (5,4) = zBottom (5,5) = 0: |10 - 0| > |0 - 0|, so (0 + 0) / 2.
        _map.SetLand(4, 4, 3, 10);

        Assert.Equal(0, CreateService().GetAverageZ(MapType.Felucca, 4, 4));
    }

    [Fact]
    public void GetAverageZ_NegativeOddSum_RoundsDown()
    {
        // zTop = -3, zBottom = 0 (difference 3); zLeft = -1, zRight = -2 (difference 1): floor((-1 - 2) / 2) = -2.
        _map.SetLand(4, 4, 3, -3).SetLand(4, 5, 3, -1).SetLand(5, 4, 3, -2);

        Assert.Equal(-2, CreateService().GetAverageZ(MapType.Felucca, 4, 4));
    }

    [Fact]
    public void GetAverageZ_MapEdge_CountsOutsideCornersAsZero()
    {
        _map.SetLand(15, 15, 3, 7);

        Assert.Equal(0, CreateService().GetAverageZ(MapType.Felucca, 15, 15));
    }

    [Fact]
    public void GetAverageZ_MapNotLoaded_ThrowsKeyNotFoundException()
    {
        Assert.Throws<KeyNotFoundException>(() => CreateService().GetAverageZ(MapType.Trammel, 1, 1));
    }

    private MovementService CreateService()
    {
        return new(_map, _tiles);
    }
}
