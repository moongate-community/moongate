using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Decorations;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Decorations;
using Moongate.Tests.TestSupport.Ultima.Maps;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class DoorGeneratorServiceTests
{
    // Frames of one side only, from ModernUO's DoorGenerator.
    private const ushort WestFrame = 0x000C;
    private const ushort EastFrame = 0x000A;
    private const ushort NorthFrame = 0x000D;
    private const ushort SouthFrame = 0x000B;
    private const ushort Wall = 0x0300;
    private const ushort Floor = 0x0301;

    private static readonly Rectangle2D Area = new(0, 0, 64, 64);

    private readonly FakeMapService _map = new(64, 64);
    private readonly FakeTileDataService _tiles = new FakeTileDataService().Item(Wall, TileFlagType.Impassable, 20)
                                                                           .Item(Floor, TileFlagType.Surface, 0);

    [Fact]
    public void Scan_AWestAndAnEastFrameTwoCellsApart_GiveOneDoorBetweenThem()
    {
        _map.AddStatic(10, 10, WestFrame, 0).AddStatic(12, 10, EastFrame, 0);

        Assert.Equal([new GeneratedDoor(new(11, 10, 0), DoorFacingType.WestCW)], Scan());
    }

    [Fact]
    public void Scan_AWestAndAnEastFrameThreeCellsApart_GiveADoubleDoor()
    {
        _map.AddStatic(10, 10, WestFrame, 0).AddStatic(13, 10, EastFrame, 0);

        Assert.Equal(
            [new GeneratedDoor(new(11, 10, 0), DoorFacingType.WestCW), new GeneratedDoor(new(12, 10, 0), DoorFacingType.EastCCW)],
            Scan()
        );
    }

    [Fact]
    public void Scan_ANorthAndASouthFrameTwoCellsApart_GiveOneDoorBetweenThem()
    {
        _map.AddStatic(10, 10, NorthFrame, 0).AddStatic(10, 12, SouthFrame, 0);

        Assert.Equal([new GeneratedDoor(new(10, 11, 0), DoorFacingType.SouthCW)], Scan());
    }

    [Fact]
    public void Scan_ANorthAndASouthFrameThreeCellsApart_GiveADoubleDoor()
    {
        _map.AddStatic(10, 10, NorthFrame, 0).AddStatic(10, 13, SouthFrame, 0);

        Assert.Equal(
            [new GeneratedDoor(new(10, 11, 0), DoorFacingType.NorthCCW), new GeneratedDoor(new(10, 12, 0), DoorFacingType.SouthCW)],
            Scan()
        );
    }

    [Fact]
    public void Scan_AFrameWithoutItsOpposite_GivesNoDoor()
    {
        _map.AddStatic(10, 10, WestFrame, 0).AddStatic(14, 10, EastFrame, 0);

        Assert.Empty(Scan());
    }

    [Fact]
    public void Scan_FramesOnDifferentFloors_GiveNoDoor()
    {
        _map.AddStatic(10, 10, WestFrame, 0).AddStatic(12, 10, EastFrame, 20);

        Assert.Empty(Scan());
    }

    [Fact]
    public void Scan_FramesOneStepApartInHeight_PutTheDoorOnTheLowerOne()
    {
        _map.SetLandZ(0, 0, 63, 63, -1).AddStatic(10, 10, WestFrame, 0).AddStatic(12, 10, EastFrame, -1);

        Assert.Equal([new GeneratedDoor(new(11, 10, -1), DoorFacingType.WestCW)], Scan());
    }

    [Fact]
    public void Scan_ADoorwayOnAnUpperFloor_StandsOnItsStaticFloor()
    {
        _map.AddStatic(10, 10, WestFrame, 20).AddStatic(12, 10, EastFrame, 20).AddStatic(11, 10, Floor, 20);

        Assert.Equal([new GeneratedDoor(new(11, 10, 20), DoorFacingType.WestCW)], Scan());
    }

    [Fact]
    public void Scan_ADoorwayWithoutAFloor_GivesNoDoor()
    {
        _map.AddStatic(10, 10, WestFrame, 20).AddStatic(12, 10, EastFrame, 20);

        Assert.Empty(Scan());
    }

    [Fact]
    public void Scan_AWalledUpDoorway_GivesNoDoor()
    {
        _map.AddStatic(10, 10, WestFrame, 0).AddStatic(12, 10, EastFrame, 0).AddStatic(11, 10, Wall, 0);

        Assert.Empty(Scan());
    }

    [Fact]
    public void Scan_ADoubleDoorwayWithOneHalfBlocked_GivesNoDoorAtAll()
    {
        _map.AddStatic(10, 10, WestFrame, 0).AddStatic(13, 10, EastFrame, 0).AddStatic(12, 10, Wall, 0);

        Assert.Empty(Scan());
    }

    [Fact]
    public void Scan_OnlyTheFramesInsideTheChunk_AreStartedFrom_ButTheirOppositeMayLieOutside()
    {
        _map.AddStatic(10, 10, WestFrame, 0).AddStatic(12, 10, EastFrame, 0).AddStatic(30, 30, WestFrame, 0).AddStatic(32, 30, EastFrame, 0);

        Assert.Equal([new GeneratedDoor(new(11, 10, 0), DoorFacingType.WestCW)], Scan(new(0, 0, 11, 64)));
    }

    [Fact]
    public void Scan_TheDoorwaysModernUoLeavesOpen_GiveNoDoor()
    {
        var map = new FakeMapService(1400, 1700);
        map.AddStatic(1342, 1743, WestFrame, 0).AddStatic(1344, 1743, EastFrame, 0);
        var generator = new DoorGeneratorService(map, _tiles);

        Assert.Empty(generator.Scan(MapType.Felucca, new(1340, 1740, 8, 8)));
    }

    [Fact]
    public void ChunksOf_ALoadedMap_CoverItsDoorRegionsInsideTheMap_InSmallPieces()
    {
        var generator = new DoorGeneratorService(new FakeMapService(1000, 1000), _tiles);

        var chunks = generator.ChunksOf(MapType.Felucca);

        // Of ModernUO's Britannia regions only (250, 750)-(775, 1330) and (2375, 330)-(3100, 1045) touch a 1000x1000 map; the
        // second starts past its edge.
        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk => Assert.True(chunk is { Width: > 0 and <= 128, Height: > 0 and <= 128 }));
        Assert.Equal((775 - 250) * (1000 - 750), chunks.Sum(chunk => chunk.Width * chunk.Height));
        Assert.Equal(250, chunks.Min(chunk => chunk.X));
        Assert.Equal(1000, chunks.Max(chunk => chunk.Y + chunk.Height));
    }

    [Fact]
    public void ChunksOf_AMapThatIsNotLoaded_OrHasNoDoorRegions_IsEmpty()
    {
        var generator = new DoorGeneratorService(_map, _tiles);

        Assert.Empty(generator.ChunksOf(MapType.Trammel));
        Assert.Empty(generator.ChunksOf(MapType.Tokuno));
    }

    private IReadOnlyList<GeneratedDoor> Scan(Rectangle2D? chunk = null)
    {
        return new DoorGeneratorService(_map, _tiles).Scan(MapType.Felucca, chunk ?? Area);
    }
}
