using Moongate.Server.Services.World;
using Moongate.UO.Data.World;

namespace Moongate.Tests.Server.World;

/// <summary>
/// A decoration group is one declaration and every spot it stands in — the shape the .cfg had, kept
/// through the conversion because 8,202 groups read better than 41,013 flat entries and store smaller
/// for the same world. Expansion is where a group becomes the objects the world will hold.
/// </summary>
public class DecorationExpansionTests
{
    [Fact]
    public void AGroup_BecomesOneObjectPerCoordinate()
    {
        var group = new DecorationGroup
        {
            Type = "Static",
            ItemId = 0x0753,
            Hue = 0x849,
            At = [[555, 425, -13], [556, 425, -13]]
        };

        var placed = DecorationCatalog.Expand(1, [group]);

        Assert.Equal(2, placed.Count);
        Assert.All(placed, p => Assert.Equal(0x0753, p.ItemId));
        Assert.All(placed, p => Assert.Equal(0x849, p.Hue));
        Assert.All(placed, p => Assert.Equal(1, p.MapId));
    }

    [Fact]
    public void AGroup_KeepsEachCoordinateExactly()
    {
        var group = new DecorationGroup { Type = "Static", ItemId = 1, At = [[10, 20, -30]] };

        var placed = Assert.Single(DecorationCatalog.Expand(0, [group]));

        Assert.Equal(10, placed.Point.X);
        Assert.Equal(20, placed.Point.Y);
        Assert.Equal(-30, placed.Point.Z);
    }

    // Z is signed and often deeply negative underground; read as unsigned it would put a whole dungeon
    // on the surface.
    [Fact]
    public void ANegativeZ_SurvivesExpansion()
        => Assert.Equal(
            -60,
            Assert.Single(DecorationCatalog.Expand(0, [new DecorationGroup { ItemId = 1, At = [[1, 1, -60]] }])).Point.Z
        );

    [Fact]
    public void TheDeclaredType_TravelsWithEachObject()
        => Assert.Equal(
            "MetalDoor",
            Assert.Single(
                DecorationCatalog.Expand(0, [new DecorationGroup { Type = "MetalDoor", ItemId = 1, At = [[1, 1, 0]] }])
            ).Type
        );

    // A malformed coordinate is data, not a crash: a hundred files will eventually carry one, and it
    // should cost one object rather than a whole facet.
    [Fact]
    public void ACoordinateThatIsNotATriple_IsSkipped()
    {
        var group = new DecorationGroup { ItemId = 1, At = [[1, 1], [2, 2, 2], []] };

        Assert.Single(DecorationCatalog.Expand(0, [group]));
    }

    [Fact]
    public void NoGroups_IsAnEmptyListNotANull()
        => Assert.Empty(DecorationCatalog.Expand(0, []));
}
