using Moongate.Server.Services.World;
using Moongate.UO.Data.World;

namespace Moongate.Tests.Server.World;

/// <summary>The catalogue is the whole world's decoration, gathered one facet at a time.</summary>
public class DecorationCatalogTests
{
    [Fact]
    public void AnEmptyCatalog_HasNothingInIt()
        => Assert.Empty(new DecorationCatalog().All);

    [Fact]
    public void AddedGroups_AreExpandedIntoTheCatalogue()
    {
        var catalog = new DecorationCatalog();

        catalog.Add(1, [new DecorationGroup { ItemId = 5, At = [[1, 1, 0], [2, 2, 0]] }]);

        Assert.Equal(2, catalog.All.Count);
    }

    // Each facet's file is added separately: the catalogue is the world, not the last file read.
    [Fact]
    public void GroupsFromSeveralMaps_AllSurvive()
    {
        var catalog = new DecorationCatalog();

        catalog.Add(0, [new DecorationGroup { ItemId = 5, At = [[1, 1, 0]] }]);
        catalog.Add(1, [new DecorationGroup { ItemId = 6, At = [[2, 2, 0]] }]);

        Assert.Equal([0, 1], catalog.All.Select(p => p.MapId).Order());
    }
}
