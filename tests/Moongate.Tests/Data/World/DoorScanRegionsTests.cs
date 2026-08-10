using Moongate.UO.Data.World;

namespace Moongate.Tests.Data.World;

public class DoorScanRegionsTests
{
    [Fact]
    public void TheMirrorFacets_AreSearchedInTheSamePlaces()
        => Assert.Equal(DoorScanRegions.For(0), DoorScanRegions.For(1));

    // The one that covers the bank a character logs in beside, and the reason any of this exists.
    [Fact]
    public void TheBritanniaRegions_CoverTheTownsOnBothMirrorFacets()
        => Assert.Contains(
            DoorScanRegions.For(1),
            region => region.StartX <= 3492 && 3492 < region.EndX && region.StartY <= 2572 && 2572 < region.EndY
        );

    [Fact]
    public void IlshenarAndMalas_AreSearchedWhole()
    {
        Assert.Equal(new(0, 0, 2304, 1600), Assert.Single(DoorScanRegions.For(2)));
        Assert.Equal(new(0, 0, 2560, 2048), Assert.Single(DoorScanRegions.For(3)));
    }

    // Tokuno and Ter Mur have no regions upstream, and inventing them is not this work.
    [Theory, InlineData(4), InlineData(5), InlineData(99)]
    public void AFacetNobodyMapped_IsSearchedNowhere(int mapId)
        => Assert.Empty(DoorScanRegions.For(mapId));

    [Fact]
    public void TheMapsAreTheOnesWithRegions()
        => Assert.Equal([0, 1, 2, 3], DoorScanRegions.Maps);

    // Every region must be a rectangle with area, or a typo silently searches nothing.
    [Fact]
    public void EveryRegion_HasArea()
        => Assert.All(
            DoorScanRegions.Maps.SelectMany(DoorScanRegions.For),
            region =>
            {
                Assert.True(region.EndX > region.StartX, $"{region} has no width");
                Assert.True(region.EndY > region.StartY, $"{region} has no height");
            }
        );
}
