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

    [Fact]
    public void CheckMovement_FlatLand_KeepsTheZ()
    {
        Assert.True(CreateService().CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, MovementAbilityType.Walk, out var newZ));
        Assert.Equal(0, newZ);
    }

    [Fact]
    public void CheckMovement_WallAhead_IsBlocked()
    {
        _tiles.Item(0x64, TileFlagType.Impassable, 20);
        _map.AddStatic(6, 5, 0x64, 0);

        Assert.False(CreateService().CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, MovementAbilityType.Walk, out var newZ));
        Assert.Equal(0, newZ);
    }

    [Theory, InlineData(2, true), InlineData(3, false)]
    public void CheckMovement_RaisedLand_ClimbsAtMostTwo(sbyte height, bool allowed)
    {
        _map.SetLandZ(6, 0, 15, 15, height);

        Assert.Equal(allowed, CreateService().CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, MovementAbilityType.Walk, out var newZ));
        Assert.Equal(allowed ? height : 0, newZ);
    }

    [Fact]
    public void CheckMovement_FloorStaticTooHigh_IsBlocked()
    {
        _tiles.Item(0x519, TileFlagType.Surface, 0);
        _map.SetLandId(6, 5, 6, 5, 2).AddStatic(6, 5, 0x519, 3);

        Assert.False(CreateService().CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, MovementAbilityType.Walk, out _));
    }

    [Fact]
    public void CheckMovement_Stair_LandsAtHalfItsHeight()
    {
        _tiles.Item(0x700, TileFlagType.Surface | TileFlagType.Bridge, 10);
        _map.AddStatic(6, 5, 0x700, 0);

        Assert.True(CreateService().CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, MovementAbilityType.Walk, out var newZ));
        Assert.Equal(5, newZ);
    }

    [Theory, InlineData(MovementAbilityType.Walk, false), InlineData(MovementAbilityType.Swim, true)]
    public void CheckMovement_Water_OnlySwimmersEnter(MovementAbilityType ability, bool allowed)
    {
        _tiles.Land(0xA8, TileFlagType.Wet | TileFlagType.Impassable);
        _map.SetLandId(6, 0, 15, 15, 0xA8);

        Assert.Equal(allowed, CreateService().CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, ability, out _));
    }

    [Fact]
    public void CheckMovement_IgnoredLandWithFloor_UsesTheFloor()
    {
        _tiles.Item(0x519, TileFlagType.Surface, 0);
        _map.SetLandId(6, 5, 6, 5, 2).AddStatic(6, 5, 0x519, 0);

        Assert.True(CreateService().CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, MovementAbilityType.Walk, out var newZ));
        Assert.Equal(0, newZ);
    }

    [Fact]
    public void CheckMovement_IgnoredLandWithoutFloor_IsBlocked()
    {
        _map.SetLandId(6, 5, 6, 5, 2);

        Assert.False(CreateService().CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, MovementAbilityType.Walk, out _));
    }

    [Fact]
    public void CheckMovement_RunningFlag_IsIgnored()
    {
        _map.SetLandZ(6, 0, 15, 15, 2);

        Assert.True(CreateService().CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East | DirectionType.Running, MovementAbilityType.Walk, out var newZ));
        Assert.Equal(2, newZ);
    }

    [Fact]
    public void CheckMovement_ForwardOutsideMap_ReturnsFalse()
    {
        Assert.False(CreateService().CheckMovement(MapType.Felucca, new(15, 5, 4), DirectionType.East, MovementAbilityType.Walk, out var newZ));
        Assert.Equal(4, newZ);
    }

    [Fact]
    public void CheckMovement_StartOutsideMap_ReturnsFalse()
    {
        Assert.False(CreateService().CheckMovement(MapType.Felucca, new(-1, 5, 4), DirectionType.East, MovementAbilityType.Walk, out var newZ));
        Assert.Equal(4, newZ);
    }

    private MovementService CreateService()
    {
        return new(_map, _tiles);
    }
}
