using Moongate.UO.Data.Types;
using Moongate.UO.Data.World;

namespace Moongate.Tests.Data.World;

/// <summary>
/// Finding the doorways the map art already draws.
/// <para>
/// Almost every door a player meets — the bank, the shops, every house — is not in the decoration
/// corpus at all: it is a gap between two door frames drawn into the map's static art, with no door
/// in it. This is how those gaps are found, ported from moongatev2's generator, which is RunUO's
/// <c>[doorgen</c>.
/// </para>
/// <para>
/// Pure, and given its statics through a delegate, so the whole algorithm is exercised against a
/// hand-built doorway rather than against six million tiles of a real map.
/// </para>
/// </summary>
public class DoorScanTests
{
    private const int WestFrame = 0x0007;
    private const int EastFrame = 0x000A;
    private const int NorthFrame = 0x0006;
    private const int SouthFrame = 0x0008;

    [Fact]
    public void AWestFrameFacingAnEastFrameTwoTilesOn_IsASingleDoorway()
    {
        var placements = DoorScan.Scan(1, Region(10, 10, 14, 12), Statics((10, 10, 0, WestFrame), (12, 10, 0, EastFrame)));

        var door = Assert.Single(placements);

        Assert.Equal(11, door.Point.X);
        Assert.Equal(10, door.Point.Y);
        Assert.Equal(DoorFacingType.WestCW, door.Facing);
    }

    // Three tiles apart is a double doorway: two leaves meeting in the middle.
    [Fact]
    public void AWestFrameThreeTilesOn_IsAPairOfDoors()
    {
        var placements = DoorScan.Scan(1, Region(10, 10, 15, 12), Statics((10, 10, 0, WestFrame), (13, 10, 0, EastFrame)));

        Assert.Equal(2, placements.Count);
        Assert.Equal(DoorFacingType.WestCW, placements[0].Facing);
        Assert.Equal(DoorFacingType.EastCCW, placements[1].Facing);
        Assert.Equal([11, 12], placements.Select(p => p.Point.X).Order());
    }

    [Fact]
    public void ANorthFrameFacingASouthFrame_IsADoorwayTheOtherWayRound()
    {
        var placements = DoorScan.Scan(1, Region(10, 10, 12, 14), Statics((10, 10, 0, NorthFrame), (10, 12, 0, SouthFrame)));

        var door = Assert.Single(placements);

        Assert.Equal(10, door.Point.X);
        Assert.Equal(11, door.Point.Y);
        Assert.Equal(DoorFacingType.SouthCW, door.Facing);
    }

    // A frame with nothing opposite is a wall decoration, not a doorway.
    [Fact]
    public void AFrameWithNoMatchOpposite_IsNotADoorway()
        => Assert.Empty(DoorScan.Scan(1, Region(10, 10, 14, 12), Statics((10, 10, 0, WestFrame))));

    // Two frames at different heights are on different floors, not either side of one doorway.
    [Fact]
    public void FramesMoreThanOneApartInZ_AreNotTheSameDoorway()
        => Assert.Empty(
            DoorScan.Scan(1, Region(10, 10, 14, 12), Statics((10, 10, 0, WestFrame), (12, 10, 20, EastFrame)))
        );

    // One step of slope between the two sides is still one doorway.
    [Fact]
    public void FramesOneApartInZ_AreStillTheSameDoorway()
        => Assert.Single(
            DoorScan.Scan(1, Region(10, 10, 14, 12), Statics((10, 10, 0, WestFrame), (12, 10, 1, EastFrame)))
        );

    // Overlapping scans of a shared wall must not put two doors in one gap.
    [Fact]
    public void TheSameDoorwayFoundTwice_YieldsOneDoor()
    {
        var statics = Statics((10, 10, 0, WestFrame), (12, 10, 0, EastFrame), (12, 10, 0, WestFrame), (14, 10, 0, EastFrame));

        var placements = DoorScan.Scan(1, Region(10, 10, 16, 12), statics);

        Assert.Equal(2, placements.Count);
        Assert.Equal([11, 13], placements.Select(p => p.Point.X).Order());
    }

    [Fact]
    public void AnEmptyRegion_IsNoDoorsRatherThanNull()
        => Assert.Empty(DoorScan.Scan(1, Region(0, 0, 10, 10), Statics()));

    // The door has to know which frame made it, or the wall's material cannot pick its template.
    [Fact]
    public void ADoorway_RemembersTheFrameThatFoundIt()
        => Assert.Equal(
            WestFrame,
            Assert.Single(
                DoorScan.Scan(1, Region(10, 10, 14, 12), Statics((10, 10, 0, WestFrame), (12, 10, 0, EastFrame)))
            ).FrameId
        );

    // Windows are frames too, and the reference generators hang doors in them.
    [Fact]
    public void AFrameTheCallerRejects_IsNotADoorway()
        => Assert.Empty(
            DoorScan.Scan(
                1,
                Region(10, 10, 14, 12),
                Statics((10, 10, 0, WestFrame), (12, 10, 0, EastFrame)),
                isDoorwayFrame: _ => false
            )
        );

    private static DoorScanRegion Region(int x1, int y1, int x2, int y2)
        => new(x1, y1, x2, y2);

    /// <summary>A map made of the tiles a test names, and nothing else.</summary>
    private static Func<int, int, IReadOnlyList<(int Id, int Z)>> Statics(
        params (int X, int Y, int Z, int Id)[] tiles
    )
        => (x, y) => tiles.Where(t => t.X == x && t.Y == y)
                          .Select(t => (t.Id, t.Z))
                          .ToList();
}
