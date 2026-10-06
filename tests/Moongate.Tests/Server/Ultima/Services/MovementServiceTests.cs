using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Tests.TestSupport.Ultima.Maps;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class MovementServiceTests
{
    private readonly FakeMapService _map = new(16, 16);
    private const int Door = 0x0675;
    private const int Crate = 0x0E3D;
    private const int Coins = 0x0EED;
    private const int Step2 = 0x0721;
    private const int Platform = 0x0519;
    private const int Stair = 0x0722;

    private readonly FakeTileDataService _tiles = new();
    private readonly SectorService _sectors = TestSectors.Create();
    private uint _nextSerial = 0x40000001;

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
        Assert.True(
            CreateService()
                .CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, MovementAbilityType.Walk, out var newZ)
        );
        Assert.Equal(0, newZ);
    }

    [Fact]
    public void CheckMovement_WallAhead_IsBlocked()
    {
        _tiles.Item(0x64, TileFlagType.Impassable, 20);
        _map.AddStatic(6, 5, 0x64, 0);

        Assert.False(
            CreateService()
                .CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, MovementAbilityType.Walk, out var newZ)
        );
        Assert.Equal(0, newZ);
    }

    [Theory, InlineData(2, true), InlineData(3, false)]
    public void CheckMovement_RaisedLand_ClimbsAtMostTwo(sbyte height, bool allowed)
    {
        _map.SetLandZ(6, 0, 15, 15, height);

        Assert.Equal(
            allowed,
            CreateService()
                .CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, MovementAbilityType.Walk, out var newZ)
        );
        Assert.Equal(allowed ? height : 0, newZ);
    }

    [Fact]
    public void CheckMovement_FloorStaticTooHigh_IsBlocked()
    {
        _tiles.Item(0x519, TileFlagType.Surface, 0);
        _map.SetLandId(6, 5, 6, 5, 2).AddStatic(6, 5, 0x519, 3);

        Assert.False(
            CreateService().CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, MovementAbilityType.Walk, out _)
        );
    }

    [Fact]
    public void CheckMovement_Stair_LandsAtHalfItsHeight()
    {
        _tiles.Item(0x700, TileFlagType.Surface | TileFlagType.Bridge, 10);
        _map.AddStatic(6, 5, 0x700, 0);

        Assert.True(
            CreateService()
                .CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, MovementAbilityType.Walk, out var newZ)
        );
        Assert.Equal(5, newZ);
    }

    [Theory, InlineData(MovementAbilityType.Walk, false), InlineData(MovementAbilityType.Swim, true)]
    public void CheckMovement_Water_OnlySwimmersEnter(MovementAbilityType ability, bool allowed)
    {
        _tiles.Land(0xA8, TileFlagType.Wet | TileFlagType.Impassable);
        _map.SetLandId(6, 0, 15, 15, 0xA8);

        Assert.Equal(
            allowed,
            CreateService().CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, ability, out _)
        );
    }

    [Fact]
    public void CheckMovement_IgnoredLandWithFloor_UsesTheFloor()
    {
        _tiles.Item(0x519, TileFlagType.Surface, 0);
        _map.SetLandId(6, 5, 6, 5, 2).AddStatic(6, 5, 0x519, 0);

        Assert.True(
            CreateService()
                .CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, MovementAbilityType.Walk, out var newZ)
        );
        Assert.Equal(0, newZ);
    }

    [Fact]
    public void CheckMovement_IgnoredLandWithoutFloor_IsBlocked()
    {
        _map.SetLandId(6, 5, 6, 5, 2);

        Assert.False(
            CreateService().CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, MovementAbilityType.Walk, out _)
        );
    }

    [Fact]
    public void CheckMovement_RunningFlag_IsIgnored()
    {
        _map.SetLandZ(6, 0, 15, 15, 2);

        Assert.True(
            CreateService()
                .CheckMovement(
                    MapType.Felucca,
                    new(5, 5, 0),
                    DirectionType.East | DirectionType.Running,
                    MovementAbilityType.Walk,
                    out var newZ
                )
        );
        Assert.Equal(2, newZ);
    }

    [Fact]
    public void CheckMovement_ForwardOutsideMap_ReturnsFalse()
    {
        Assert.False(
            CreateService()
                .CheckMovement(MapType.Felucca, new(15, 5, 4), DirectionType.East, MovementAbilityType.Walk, out var newZ)
        );
        Assert.Equal(4, newZ);
    }

    [Fact]
    public void CheckMovement_StartOutsideMap_ReturnsFalse()
    {
        Assert.False(
            CreateService()
                .CheckMovement(MapType.Felucca, new(-1, 5, 4), DirectionType.East, MovementAbilityType.Walk, out var newZ)
        );
        Assert.Equal(4, newZ);
    }

    [Fact]
    public void CheckMovement_DiagonalWithFreeSides_IsAllowed()
    {
        Assert.True(
            CreateService()
                .CheckMovement(
                    MapType.Felucca,
                    new(5, 5, 0),
                    DirectionType.NorthEast,
                    MovementAbilityType.Walk,
                    out var newZ
                )
        );
        Assert.Equal(0, newZ);
    }

    [Theory, InlineData(5, 4), InlineData(6, 5)]
    public void CheckMovement_DiagonalWithOneSideBlocked_IsBlocked(int x, int y)
    {
        // North-east from (5, 5) lands on (6, 4); its sides are north (5, 4) and east (6, 5).
        _tiles.Item(0x64, TileFlagType.Impassable, 20);
        _map.AddStatic(x, y, 0x64, 0);

        Assert.False(
            CreateService()
                .CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.NorthEast, MovementAbilityType.Walk, out _)
        );
    }

    [Fact]
    public void CheckMovement_DirectionWithJunkBits_UsesTheLowThreeBits()
    {
        // 0x09 is north-east (0x01) with a stray bit, as ModernUO masks it; north-east from (5, 5) lands on (6, 4).
        _map.SetLandZ(6, 4, 6, 4, 2).SetLandZ(7, 3, 7, 4, 2).SetLandZ(6, 3, 6, 3, 2);

        Assert.True(
            CreateService()
                .CheckMovement(MapType.Felucca, new(5, 5, 0), (DirectionType)0x09, MovementAbilityType.Walk, out var newZ)
        );
        Assert.Equal(CreateService().GetAverageZ(MapType.Felucca, 6, 4), newZ);
        Assert.NotEqual(0, newZ);
    }

    [Fact]
    public void TryGetDropZ_FlatLand_IsTheLand()
    {
        _map.SetLandZ(0, 0, 15, 15, 5);

        Assert.True(CreateService().TryGetDropZ(MapType.Felucca, 4, 4, 21, out var z));
        Assert.Equal(5, z);
    }

    [Fact]
    public void TryGetDropZ_ATable_IsItsTop()
    {
        _tiles.Item(0x0B34, TileFlagType.Surface, 6);
        _map.AddStatic(4, 4, 0x0B34, 0);

        Assert.True(CreateService().TryGetDropZ(MapType.Felucca, 4, 4, 16, out var z));
        Assert.Equal(6, z);
    }

    [Fact]
    public void TryGetDropZ_ABridge_CountsHalfItsHeight()
    {
        _tiles.Item(0x0063, TileFlagType.Surface | TileFlagType.Bridge, 10);
        _map.AddStatic(4, 4, 0x0063, 0);

        Assert.True(CreateService().TryGetDropZ(MapType.Felucca, 4, 4, 16, out var z));
        Assert.Equal(5, z);
    }

    [Fact]
    public void TryGetDropZ_ASurfaceAboveTheCeiling_IsSkipped()
    {
        _tiles.Item(0x0B34, TileFlagType.Surface, 6);
        _map.AddStatic(4, 4, 0x0B34, 20);

        Assert.True(CreateService().TryGetDropZ(MapType.Felucca, 4, 4, 16, out var z));
        Assert.Equal(0, z);
    }

    [Fact]
    public void TryGetDropZ_ImpassableLandAndNoSurface_Fails()
    {
        _tiles.Land(0x00A8, TileFlagType.Impassable);
        _map.SetLandId(4, 4, 4, 4, 0x00A8);

        Assert.False(CreateService().TryGetDropZ(MapType.Felucca, 4, 4, 16, out _));
    }

    [Fact]
    public void TryGetDropZ_OutsideTheMapOrMapNotLoaded_Fails()
    {
        Assert.False(CreateService().TryGetDropZ(MapType.Felucca, 99, 4, 16, out _));
        Assert.False(CreateService().TryGetDropZ(MapType.Trammel, 4, 4, 16, out _));
    }

    [Fact]
    public void TryGetSpawnZ_FlatLand_IsTheLand()
    {
        _map.SetLandZ(0, 0, 15, 15, 5);

        Assert.True(CreateService().TryGetSpawnZ(MapType.Felucca, 4, 4, 23, out var z));
        Assert.Equal(5, z);
    }

    [Fact]
    public void TryGetSpawnZ_AFloorUnderTheCeiling_IsTheHighestSurface()
    {
        _tiles.Item(0x0519, TileFlagType.Surface, 0);
        _map.AddStatic(4, 4, 0x0519, 10);

        Assert.True(CreateService().TryGetSpawnZ(MapType.Felucca, 4, 4, 18, out var z));
        Assert.Equal(10, z);
    }

    [Fact]
    public void TryGetSpawnZ_AFloorAboveTheCeiling_IsSkipped()
    {
        _tiles.Item(0x0519, TileFlagType.Surface, 0);
        _map.AddStatic(4, 4, 0x0519, 40);

        Assert.True(CreateService().TryGetSpawnZ(MapType.Felucca, 4, 4, 18, out var z));
        Assert.Equal(0, z);
    }

    [Fact]
    public void TryGetSpawnZ_ATreeOnTheLand_Fails()
    {
        _tiles.Item(0x0CCA, TileFlagType.Impassable, 20);
        _map.AddStatic(4, 4, 0x0CCA, 0);

        Assert.False(CreateService().TryGetSpawnZ(MapType.Felucca, 4, 4, 18, out _));
    }

    [Fact]
    public void TryGetSpawnZ_NoHeadroomOnTheHighestFloor_TakesTheOneUnderIt()
    {
        // The floor at 16 has 9 of headroom under the floor at 25; the land at 0 has the full 16.
        _tiles.Item(0x0519, TileFlagType.Surface, 0);
        _map.AddStatic(4, 4, 0x0519, 16).AddStatic(4, 4, 0x0519, 25);

        Assert.True(CreateService().TryGetSpawnZ(MapType.Felucca, 4, 4, 18, out var z));
        Assert.Equal(0, z);
    }

    [Fact]
    public void TryGetSpawnZ_NoHeadroomAnywhere_Fails()
    {
        _tiles.Item(0x0519, TileFlagType.Surface, 0);
        // Under the ceiling at 12: the land at 0 is capped by the floor at 8, the floor at 8 by the floor at 16.
        _map.AddStatic(4, 4, 0x0519, 8).AddStatic(4, 4, 0x0519, 16);

        Assert.False(CreateService().TryGetSpawnZ(MapType.Felucca, 4, 4, 12, out _));
    }

    [Fact]
    public void TryGetSpawnZ_Water_Fails()
    {
        _tiles.Land(0x00A8, TileFlagType.Impassable | TileFlagType.Wet);
        _tiles.Item(0x1797, TileFlagType.Surface | TileFlagType.Wet | TileFlagType.Impassable, 0);
        _map.SetLandId(4, 4, 4, 4, 0x00A8).AddStatic(4, 4, 0x1797, 0);

        Assert.False(CreateService().TryGetSpawnZ(MapType.Felucca, 4, 4, 18, out _));
    }

    [Fact]
    public void TryGetSpawnZ_OutsideTheMapOrMapNotLoaded_Fails()
    {
        Assert.False(CreateService().TryGetSpawnZ(MapType.Felucca, 99, 4, 18, out _));
        Assert.False(CreateService().TryGetSpawnZ(MapType.Trammel, 4, 4, 18, out _));
    }

    [Fact]
    public void TryGetSwimZ_WaterLand_IsItsZ()
    {
        _tiles.Land(0x00A8, TileFlagType.Impassable | TileFlagType.Wet);
        _map.SetLandId(0, 0, 15, 15, 0x00A8).SetLandZ(0, 0, 15, 15, -5);

        Assert.True(CreateService().TryGetSwimZ(MapType.Felucca, 4, 4, out var z));
        Assert.Equal(-5, z);
    }

    [Fact]
    public void TryGetSwimZ_DryLand_Fails()
    {
        Assert.False(CreateService().TryGetSwimZ(MapType.Felucca, 4, 4, out _));
    }

    [Fact]
    public void TryGetSwimZ_AWaterStatic_IsItsTop()
    {
        _tiles.Item(0x1797, TileFlagType.Surface | TileFlagType.Wet | TileFlagType.Impassable, 0);
        _map.AddStatic(4, 4, 0x1797, 2);

        Assert.True(CreateService().TryGetSwimZ(MapType.Felucca, 4, 4, out var z));
        Assert.Equal(2, z);
    }

    [Fact]
    public void TryGetSwimZ_WaterUnderADock_Fails()
    {
        _tiles.Land(0x00A8, TileFlagType.Impassable | TileFlagType.Wet);
        _tiles.Item(0x0519, TileFlagType.Surface, 0);
        _map.SetLandId(4, 4, 4, 4, 0x00A8).AddStatic(4, 4, 0x0519, 5);

        Assert.False(CreateService().TryGetSwimZ(MapType.Felucca, 4, 4, out _));
    }

    [Fact]
    public void TryGetSwimZ_AWaterStaticBelowTheGround_Fails()
    {
        // A swimmer there could not move: the ground at 10 rises over the water at 5.
        _tiles.Item(0x1797, TileFlagType.Surface | TileFlagType.Wet | TileFlagType.Impassable, 0);
        _map.SetLandZ(0, 0, 15, 15, 10).AddStatic(4, 4, 0x1797, 5);

        Assert.False(CreateService().TryGetSwimZ(MapType.Felucca, 4, 4, out _));
    }

    [Fact]
    public void TryGetSwimZ_BloodOrATrough_IsNotWater()
    {
        // Blood is wet but passable; a trough is wet and impassable but stands 6 high.
        _tiles.Item(0x122A, TileFlagType.Wet, 0).Item(0x0B41, TileFlagType.Wet | TileFlagType.Impassable, 6);
        _map.AddStatic(4, 4, 0x122A, 0).AddStatic(5, 5, 0x0B41, 0);

        Assert.False(CreateService().TryGetSwimZ(MapType.Felucca, 4, 4, out _));
        Assert.False(CreateService().TryGetSwimZ(MapType.Felucca, 5, 5, out _));
    }

    [Fact]
    public void TryGetSwimZ_WetLandAMoverWalksOn_IsNotWater()
    {
        _tiles.Land(0x2E0E, TileFlagType.Wet);
        _map.SetLandId(4, 4, 4, 4, 0x2E0E);

        Assert.False(CreateService().TryGetSwimZ(MapType.Felucca, 4, 4, out _));
    }

    [Fact]
    public void TryGetSwimZ_OutsideTheMapOrMapNotLoaded_Fails()
    {
        Assert.False(CreateService().TryGetSwimZ(MapType.Felucca, 99, 4, out _));
        Assert.False(CreateService().TryGetSwimZ(MapType.Trammel, 4, 4, out _));
    }

    [Fact]
    public void CheckMovement_AClosedDoorOnTheCell_Blocks_AndFreesItOnceSwungAside()
    {
        var door = Ground(Door, 6, 5, 0);

        Assert.False(Step(new(5, 5, 0), DirectionType.East, out _));

        // Open: the door stands on the next cell, as door.lua moves it.
        door.PlaceOnGround(MapType.Felucca, new Point3D(7, 4, 0));
        _sectors.AddItem(door);

        Assert.True(Step(new(5, 5, 0), DirectionType.East, out var z));
        Assert.Equal(0, z);
    }

    [Fact]
    public void CheckMovement_PassDoors_WalksThroughADoor_NotThroughAWall()
    {
        Ground(Door, 6, 5, 0);
        Ground(Crate, 5, 6, 0);

        Assert.True(
            CreateService()
                .CheckMovement(
                    MapType.Felucca,
                    new(5, 5, 0),
                    DirectionType.East,
                    MovementAbilityType.Walk | MovementAbilityType.PassDoors,
                    out _
                )
        );
        Assert.False(
            CreateService()
                .CheckMovement(
                    MapType.Felucca,
                    new(5, 5, 0),
                    DirectionType.South,
                    MovementAbilityType.Walk | MovementAbilityType.PassDoors,
                    out _
                )
        );
    }

    [Fact]
    public void CheckMovement_AnImpassableItem_Blocks_EvenWhenItCanBeMoved()
    {
        var crate = Ground(Crate, 6, 5, 0);
        crate.Movable = true;

        Assert.False(Step(new(5, 5, 0), DirectionType.East, out _));
    }

    [Fact]
    public void CheckMovement_AnItemAboveTheMoversHead_DoesNotBlock()
    {
        // A hanging sign, 20 above the floor.
        Ground(Crate, 6, 5, 20);

        Assert.True(Step(new(5, 5, 0), DirectionType.East, out _));
    }

    [Fact]
    public void CheckMovement_AnItemThatIsNeitherImpassableNorASurface_DoesNotBlock()
    {
        Ground(Coins, 6, 5, 0);

        Assert.True(Step(new(5, 5, 0), DirectionType.East, out var z));
        Assert.Equal(0, z);
    }

    [Fact]
    public void CheckMovement_ADiagonalPastADoor_IsBlocked()
    {
        Ground(Door, 6, 5, 0);

        Assert.False(Step(new(5, 5, 0), DirectionType.SouthEast, out _));
    }

    [Fact]
    public void CheckMovement_AFixedSurfaceItem_IsSteppedOnto()
    {
        Ground(Step2, 6, 5, 0).Movable = false;

        Assert.True(Step(new(5, 5, 0), DirectionType.East, out var z));
        Assert.Equal(2, z);
    }

    [Fact]
    public void CheckMovement_ASurfaceItemThatCanBeMoved_IsNotAFloor()
    {
        Ground(Step2, 6, 5, 0).Movable = true;

        // Not stood on, and in the way of the ground under it.
        Assert.False(Step(new(5, 5, 0), DirectionType.East, out _));
    }

    [Fact]
    public void CheckMovement_FromAPlatformOfItems_GoesOnAtItsHeight()
    {
        Ground(Platform, 5, 5, 10).Movable = false;
        Ground(Platform, 6, 5, 10).Movable = false;

        Assert.True(Step(new(5, 5, 10), DirectionType.East, out var z));
        Assert.Equal(10, z);
    }

    [Fact]
    public void CheckMovement_FromAPlatformOfItems_HasItsHeadroomAboveThePlatform()
    {
        Ground(Platform, 5, 5, 10).Movable = false;
        Ground(Platform, 6, 5, 10).Movable = false;
        // A beam 22 above the land: over the head of who stands on the platform at 10, in the face of who would be
        // taken to stand on the land.
        Ground(Crate, 6, 5, 22);

        Assert.False(Step(new(5, 5, 10), DirectionType.East, out _));

        _sectors.RemoveItem(_sectors.GetItemsAt(MapType.Felucca, 6, 5)[1]);
        Ground(Crate, 6, 5, 27);

        Assert.True(Step(new(5, 5, 10), DirectionType.East, out var z));
        Assert.Equal(10, z);
    }

    [Fact]
    public void CheckMovement_AnItemsTemplate_SaysWhetherItIsAFloor()
    {
        // As what .decorate places: the item has no word of its own, its template says it cannot be picked up.
        Ground(Step2, 6, 5, 0, "decoration");
        Ground(Step2, 5, 6, 0, "loose_plank");
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "decoration", ItemId = new Serial(Step2), Movable = false },
                new ItemTemplate { Id = "loose_plank", ItemId = new Serial(Step2), Movable = true }
            )
        );
        CreateService();
        var service = new MovementService(_map, _tiles, _sectors, templates);

        Assert.True(
            service.CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.East, MovementAbilityType.Walk, out var z)
        );
        Assert.Equal(2, z);
        Assert.False(
            service.CheckMovement(MapType.Felucca, new(5, 5, 0), DirectionType.South, MovementAbilityType.Walk, out _)
        );
    }

    [Fact]
    public void CheckMovement_AnItemOfAnUnknownTemplate_IsAFloorWhenItsGraphicCannotBeLifted()
    {
        Ground(Stair, 6, 5, 0, "renamed");

        Assert.True(Step(new(5, 5, 0), DirectionType.East, out var z));
        Assert.Equal(2, z);
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(16, true)]
    public void CheckMovement_ADoorAtAnotherHeight_BlocksOnlyWhenInTheWay(int z, bool allowed)
    {
        Ground(Door, 6, 5, z);

        Assert.Equal(allowed, Step(new(5, 5, 0), DirectionType.East, out _));
    }

    [Fact]
    public void CheckMovement_AnItemWithAGraphicOutOfRange_IsIgnored()
    {
        Ground(0x20000, 6, 5, 0);

        Assert.True(Step(new(5, 5, 0), DirectionType.East, out _));
    }

    [Fact]
    public void CheckMovement_WithoutSectors_SeesNoItem()
    {
        Ground(Door, 6, 5, 0);

        Assert.True(
            new MovementService(_map, _tiles).CheckMovement(
                MapType.Felucca,
                new(5, 5, 0),
                DirectionType.East,
                MovementAbilityType.Walk,
                out _
            )
        );
    }

    private bool Step(Point3D from, DirectionType direction, out int newZ)
    {
        return CreateService().CheckMovement(MapType.Felucca, from, direction, MovementAbilityType.Walk, out newZ);
    }

    private ItemEntity Ground(int graphic, int x, int y, int z, string template = "thing")
    {
        var item = new ItemEntity { Id = new Serial(_nextSerial++), TemplateId = template, ItemId = graphic, Amount = 1 };
        item.PlaceOnGround(MapType.Felucca, new Point3D(x, y, z));
        _sectors.AddItem(item);

        return item;
    }

    private MovementService CreateService()
    {
        _tiles.Item(Door, TileFlagType.Impassable | TileFlagType.Door, 20)
            .Item(Crate, TileFlagType.Impassable, 10)
            .Item(Coins, TileFlagType.Generic, 0)
            .Item(Step2, TileFlagType.Surface, 2)
            .Item(Platform, TileFlagType.Surface, 0)
            .Item(Stair, TileFlagType.Surface, 2, 255);

        return new(_map, _tiles, _sectors);
    }
}
