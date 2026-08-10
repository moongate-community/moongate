using Moongate.Server.Services.Items;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.UO.Data.Items;
using Moongate.UO.Data.Signs;
using Moongate.UO.Data.Types;
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

        var (placed, skipped, _) = service.Place();

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
        var (placed, skipped, _) = service.Place();

        Assert.Equal(0, placed);
        Assert.Equal(2, skipped);
        Assert.Equal(2, items.GetItemsInRange(1, new(10, 10, 0), 5).Count);
    }

    /// <summary>
    /// A shard whose <c>templates/items/</c> already existed never received <c>world_decoration.yaml</c>
    /// — the loader seeds that directory only when it is absent, deliberately, so an operator's
    /// curated set is left alone. Decoration must not be hostage to that: its template is an
    /// implementation detail of this service, not content anyone curates, so the service supplies its
    /// own when the registry has none. A shipped YAML still wins where one exists.
    /// </summary>
    [Fact]
    public void Place_WhenTheTemplateIsNotRegistered_UsesItsOwnAndStillPlaces()
    {
        var (service, spatial) = Build(
            [new DecorationGroup { ItemId = 100, Hue = 7, At = [[10, 10, 0]] }],
            registerTemplate: false
        );

        var (placed, _, _) = service.Place();

        Assert.Equal(1, placed);
        var item = Assert.Single(spatial.GetItemsInRange(1, new(10, 10, 0), 0));
        Assert.Equal(100, item.ItemId);
        Assert.Equal(7, item.Hue.Value);
    }

    // The operator's YAML is authoritative where it exists: the built-in is a fallback, not an override.
    [Fact]
    public void Place_WhenTheTemplateIsRegistered_DoesNotReplaceIt()
    {
        var templates = new ItemTemplateService();
        templates.Register(
            new ItemTemplate
            {
                Id = DecorationPlacementService.TemplateId, Name = "Operator override", Category = "World", ItemId = 1
            }
        );
        var (service, _) = Build([new DecorationGroup { ItemId = 100, At = [[10, 10, 0]] }], templates: templates);

        service.Place();

        Assert.Equal("Operator override", templates.GetById(DecorationPlacementService.TemplateId)!.Name);
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

    [Fact]
    public void Place_ASignWithACliloc_LandsCarryingThatCliloc()
    {
        var (service, spatial) = Build(
            [],
            signs:
            [
                new SignEntry
                {
                    Map = MapType.Trammel, ItemId = 3032, X = 10, Y = 10, Z = 0, Label = "#1016093"
                }
            ]
        );

        Assert.Equal(1, service.Place().Placed);

        var sign = Assert.Single(spatial.GetItemsInRange((int)MapType.Trammel, new(10, 10, 0), 0));

        Assert.Equal(3032, sign.ItemId);
        Assert.Equal(1016093, sign.NameCliloc);
        Assert.Equal("", sign.Name);
    }

    // 59 of the 509 shipped signs carry their words rather than a cliloc.
    [Fact]
    public void Place_ASignWithLiteralText_LandsCarryingThatName()
    {
        var (service, spatial) = Build(
            [],
            signs:
            [
                new SignEntry
                {
                    Map = MapType.Felucca, ItemId = 3032, X = 5, Y = 5, Z = 0, Label = "The Shakin' Bakery"
                }
            ]
        );

        service.Place();

        var sign = Assert.Single(spatial.GetItemsInRange((int)MapType.Felucca, new(5, 5, 0), 0));

        Assert.Equal("The Shakin' Bakery", sign.Name);
        Assert.Equal(0, sign.NameCliloc);
    }

    // Signs go through the same check as decoration: typing decorate twice must not double them.
    [Fact]
    public void Place_RunTwice_DoesNotDoubleTheSigns()
    {
        var (service, spatial) = Build(
            [],
            signs:
            [
                new SignEntry { Map = MapType.Felucca, ItemId = 3032, X = 5, Y = 5, Z = 0, Label = "#1016093" }
            ]
        );

        service.Place();
        var (placed, skipped, _) = service.Place();

        Assert.Equal(0, placed);
        Assert.Equal(1, skipped);
        Assert.Single(spatial.GetItemsInRange((int)MapType.Felucca, new(5, 5, 0), 0));
    }

    // Both sources report through one pair of numbers, because one command placed both.
    [Fact]
    public void Place_CountsDecorationAndSignsTogether()
    {
        var (service, _) = Build(
            [new DecorationGroup { ItemId = 100, At = [[1, 1, 0]] }],
            signs:
            [
                new SignEntry { Map = MapType.Trammel, ItemId = 3032, X = 2, Y = 2, Z = 0, Label = "#1016093" }
            ]
        );

        Assert.Equal(2, service.Place().Placed);
    }

    // A door is built from the template that carries its behaviour, not from world_decoration -- and it
    // keeps the graphic the corpus placed it with, which is what tells it which way it faces.
    [Fact]
    public void Place_ADeclaredDoor_IsBuiltFromItsDoorTemplate()
    {
        var (service, spatial) = Build(
            [new DecorationGroup { Type = "MetalDoor", ItemId = 0x677, At = [[10, 10, 0]] }],
            templates: TemplatesWithDoor()
        );

        service.Place();

        var door = Assert.Single(spatial.GetItemsInRange(1, new(10, 10, 0), 0));

        Assert.Equal("metal_door", door.TemplateId);
        Assert.Equal("items.door", door.ScriptId);
        Assert.Equal(0x677, door.ItemId);
    }

    [Fact]
    public void Place_AnUndeclaredType_IsStillPlainDecoration()
    {
        var (service, spatial) = Build(
            [new DecorationGroup { Type = "LibraryBookcase", ItemId = 0xA9C, At = [[10, 10, 0]] }],
            templates: TemplatesWithDoor()
        );

        service.Place();

        Assert.Equal(
            DecorationPlacementService.TemplateId,
            Assert.Single(spatial.GetItemsInRange(1, new(10, 10, 0), 0)).TemplateId
        );
    }

    // A door that does not open is worse than furniture, but a hole in the world is worse than both.
    [Fact]
    public void Place_ADoorWhoseTemplateIsNotRegistered_FallsBackToDecoration()
    {
        var (service, spatial) = Build([new DecorationGroup { Type = "MetalDoor", ItemId = 0x677, At = [[10, 10, 0]] }]);

        Assert.Equal(1, service.Place().Placed);
        Assert.Equal(
            DecorationPlacementService.TemplateId,
            Assert.Single(spatial.GetItemsInRange(1, new(10, 10, 0), 0)).TemplateId
        );
    }

    /// <summary>
    /// The repair path: a shard decorated before doors could open holds 944 of them built from the
    /// inert template, and the idempotence check would skip them forever — it sees the right graphic
    /// standing at the right point and moves on.
    /// </summary>
    [Fact]
    public void Place_ADoorAlreadyPlacedAsDecoration_IsConvertedInPlace()
    {
        var groups = new[] { new DecorationGroup { Type = "MetalDoor", ItemId = 0x677, At = [[10, 10, 0]] } };
        var world = new World();
        var (before, spatial) = Build(groups, world: world);

        before.Place();
        var original = Assert.Single(spatial.GetItemsInRange(1, new(10, 10, 0), 0));

        var (after, _) = Build(groups, templates: TemplatesWithDoor(), world: world);
        var result = after.Place();
        var door = Assert.Single(spatial.GetItemsInRange(1, new(10, 10, 0), 0));

        Assert.Equal(0, result.Placed);
        Assert.Equal(1, result.Converted);

        // The same serial: whatever referenced it still does.
        Assert.Equal(original.Id, door.Id);
        Assert.Equal("metal_door", door.TemplateId);
        Assert.Equal("items.door", door.ScriptId);
    }

    // The one that matters. This edits items in a live world, from a command someone types twice.
    [Fact]
    public void Place_RunTwice_ConvertsNothingTheSecondTime()
    {
        var (service, _) = Build(
            [new DecorationGroup { Type = "MetalDoor", ItemId = 0x677, At = [[10, 10, 0]] }],
            templates: TemplatesWithDoor()
        );

        service.Place();
        var second = service.Place();

        Assert.Equal(0, second.Placed);
        Assert.Equal(0, second.Converted);
        Assert.Equal(1, second.Skipped);
    }

    [Fact]
    public void Place_ADecorationObjectAlreadyThere_IsNotConverted()
    {
        var (service, _) = Build([new DecorationGroup { ItemId = 100, At = [[10, 10, 0]] }]);

        service.Place();

        var second = service.Place();

        Assert.Equal(0, second.Converted);
        Assert.Equal(1, second.Skipped);
    }

    /// <summary>One world two services can be built over, so a second run sees what the first placed.</summary>
    private sealed class World
    {
        public FakePersistenceService Persistence { get; } = new();

        public SpatialIndexService? Spatial { get; set; }
    }

    private static ItemTemplateService TemplatesWithDoor()
    {
        var templates = new ItemTemplateService();

        templates.Register(
            new ItemTemplate
            {
                Id = "metal_door", Name = "", Category = "Structure", ItemId = 0x675, ScriptId = "items.door"
            }
        );

        return templates;
    }

    private static (DecorationPlacementService Service, SpatialIndexService Spatial) Build(
        IEnumerable<DecorationGroup> groups,
        bool registerTemplate = true,
        ItemTemplateService? templates = null,
        IEnumerable<SignEntry>? signs = null,
        World? world = null
    )
    {
        world ??= new();
        var persistence = world.Persistence;
        var events = new StubEventBus();
        var spatial = world.Spatial ??= new SpatialIndexService(persistence, new StubLoopAffinity(), events);
        var itemService = new ItemService(persistence, spatial: spatial);

        templates ??= new ItemTemplateService();

        if (registerTemplate && templates.GetById(DecorationPlacementService.TemplateId) is null)
        {
            templates.Register(
                new ItemTemplate
                {
                    Id = DecorationPlacementService.TemplateId, Name = "", Category = "World", ItemId = 1
                }
            );
        }

        var catalog = new DecorationCatalog();
        catalog.Add(1, groups);

        var signService = new SignService();

        foreach (var sign in signs ?? [])
        {
            signService.Register(sign);
        }

        return (
            new(catalog, new ItemFactoryService(templates, new(1)), itemService, spatial, templates, signService),
            spatial
        );
    }
}
