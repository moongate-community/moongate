using Moongate.Server.Services.Items;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.UO.Data.Items;
using Moongate.UO.Data.World;

namespace Moongate.Tests.Server.World;

/// <summary>
/// Turning the catalogue into world. The test that matters is the second run: this is driven by a
/// command someone can type twice, and a second pass would otherwise double forty thousand objects.
/// </summary>
public class DecorationPlacementTests
{
    [Fact]
    public void Place_CreatesOneItemPerPlacement()
    {
        var (service, items) = Build([new DecorationGroup { ItemId = 100, At = [[10, 10, 0], [11, 10, 0]] }]);

        var (placed, skipped) = service.Place();

        Assert.Equal(2, placed);
        Assert.Equal(0, skipped);
        Assert.Equal(2, items.GetItemsInRange(1, new(10, 10, 0), 5).Count);
    }

    // One template, 2381 appearances: the graphic and hue live on the instance.
    [Fact]
    public void Place_GivesEachItemItsOwnGraphicAndHue()
    {
        var (service, items) = Build(
            [
                new DecorationGroup { ItemId = 100, Hue = 7, At = [[10, 10, 0]] },
                new DecorationGroup { ItemId = 200, At = [[12, 10, 0]] }
            ]
        );

        service.Place();

        var placed = items.GetItemsInRange(1, new(11, 10, 0), 5);

        Assert.Contains(placed, i => i.ItemId == 100 && i.Hue.Value == 7);
        Assert.Contains(placed, i => i.ItemId == 200);
    }

    [Fact]
    public void Place_RunTwice_DoesNotDoubleTheWorld()
    {
        var (service, items) = Build([new DecorationGroup { ItemId = 100, At = [[10, 10, 0], [11, 10, 0]] }]);

        service.Place();
        var (placed, skipped) = service.Place();

        Assert.Equal(0, placed);
        Assert.Equal(2, skipped);
        Assert.Equal(2, items.GetItemsInRange(1, new(10, 10, 0), 5).Count);
    }

    // A different graphic on the same tile is a different object -- statics stack.
    [Fact]
    public void Place_ADifferentGraphicOnTheSameTile_IsStillPlaced()
    {
        var (service, items) = Build(
            [
                new DecorationGroup { ItemId = 100, At = [[10, 10, 0]] },
                new DecorationGroup { ItemId = 200, At = [[10, 10, 0]] }
            ]
        );

        Assert.Equal(2, service.Place().Placed);
    }

    private static (DecorationPlacementService Service, SpatialIndexService Spatial) Build(
        IEnumerable<DecorationGroup> groups
    )
    {
        var persistence = new FakePersistenceService();
        var events = new StubEventBus();
        var spatial = new SpatialIndexService(persistence, new StubLoopAffinity(), events);
        var itemService = new ItemService(persistence, spatial: spatial);

        var templates = new ItemTemplateService();
        templates.Register(
            new ItemTemplate
            {
                Id = DecorationPlacementService.TemplateId, Name = "", Category = "World", ItemId = 1
            }
        );

        var catalog = new DecorationCatalog();
        catalog.Add(1, groups);

        return (
            new(catalog, new ItemFactoryService(templates, new(1)), itemService, spatial),
            spatial
        );
    }
}
