using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Decorations;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Decorations;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class DecorationServiceTests
{
    private readonly SectorService _sectors;
    private readonly ItemService _items;
    private readonly FakeItemFactoryService _factory;
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingProgress<DecorationFileResult> _progress = new();

    public DecorationServiceTests()
    {
        var sectors = TestSectors.Create();
        _items = TestItems.Create(sectors);
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "decoration", ItemId = new Serial(0x0A28), Movable = false, Decays = false },
                new ItemTemplate { Id = "decoration_door", ItemId = new Serial(0x0675), Movable = false, Decays = false, ScriptId = "door" }
            )
        );
        _factory = new(templates, new FakeTileDataService());
        _sectors = sectors;
    }

    [Fact]
    public async Task DecorateAsync_PlacesAStaticOnEveryMapOfItsFile_WithItsGraphicHueNameAndOtherProps()
    {
        var block = Block("Static", 0x0063, props: new() { ["hue"] = 5L, ["name"] = "a wall", ["light"] = "Circle225" });

        var result = await Service(File("britannia", block)).DecorateAsync(_progress);

        Assert.Equal(new DecorationResult(2, 0, 0, 1), result);
        Assert.Equal(
            [MapType.Felucca, MapType.Trammel],
            _items.Items.Select(item => item.Map!.Value).Order()
        );
        Assert.All(
            _items.Items,
            item =>
            {
                Assert.Equal(("decoration", 0x0063, (ushort)5, "a wall"), (item.TemplateId, item.ItemId, item.Hue.Value, item.Name));
                Assert.Equal(new Point3D(1500, 1600, 10), item.GroundLocation);
                Assert.Equal(new Dictionary<string, object?> { ["light"] = "Circle225" }, item.Props);
            }
        );
        Assert.Equal(2, _view.Calls.Count(call => call.StartsWith("Appeared", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task DecorateAsync_PlacesADoorWithTheDoorTemplate_KeepingItsFacingAndType()
    {
        var block = Block("MetalDoor2", 0x0675, props: new() { ["facing"] = "west_cw" });

        await Service(File("trammel", block)).DecorateAsync(_progress);

        var door = Assert.Single(_items.Items);
        Assert.Equal(("decoration_door", 0x0675, (string?)null), (door.TemplateId, door.ItemId, door.Name));
        Assert.Equal(
            new Dictionary<string, object?> { ["facing"] = "west_cw", ["decoration_type"] = "MetalDoor2" },
            door.Props
        );
    }

    [Theory]
    [InlineData("MetalDoor", true)]
    [InlineData("SecretStoneDoor1", true)]
    [InlineData("IronGateShort", true)]
    [InlineData("DarkWoodGate", true)]
    [InlineData("Static", false)]
    [InlineData("LocalizedSign", false)]
    public async Task DecorateAsync_DoorAndGateKinds_UseTheDoorTemplate(string type, bool door)
    {
        await Service(File("trammel", Block(type, 0x0675))).DecorateAsync(_progress);

        Assert.Equal(door ? "decoration_door" : "decoration", Assert.Single(_items.Items).TemplateId);
    }

    [Fact]
    public async Task DecorateAsync_SkipsTheKindsThatNeedTheirOwnLogic_CountingThemByType()
    {
        var file = File(
            "trammel",
            Block("Teleporter", 0x1BC3),
            Block("KeywordTeleporter", 0x1BC3),
            Block("Spawner", 0x1F13),
            Block("MarkContainer", 0x0E80),
            Block("PublicMoongate", 0x0F6C),
            Block("AnvilEastAddon", null),
            Block("Static", 0x0063, new Point3D(1500, 1600, 10), new Point3D(1501, 1600, 10))
        );

        var result = await Service(file).DecorateAsync(_progress);

        Assert.Equal(new DecorationResult(2, 0, 6, 1), result);
        var report = Assert.Single(_progress.Reports);
        Assert.Equal(("trammel", "town", 2, 0, 6), (report.Folder, report.Name, report.Placed, report.Present, report.Skipped));
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["Teleporter"] = 1, ["KeywordTeleporter"] = 1, ["Spawner"] = 1, ["MarkContainer"] = 1, ["PublicMoongate"] = 1,
                ["AnvilEastAddon"] = 1
            },
            report.SkippedByType
        );
    }

    [Fact]
    public async Task DecorateAsync_SkipsALocationOutsideTheMap()
    {
        var result = await Service(File("trammel", Block("Static", 0x0063, new Point3D(9000, 100, 0)))).DecorateAsync(_progress);

        Assert.Equal(new DecorationResult(0, 0, 1, 1), result);
        Assert.Equal(1, Assert.Single(_progress.Reports).SkippedByType["outside the map"]);
        Assert.Empty(_items.Items);
    }

    [Fact]
    public async Task DecorateAsync_RunTwice_KeepsWhatIsThere_AndPlacesNothingNew()
    {
        var service = Service(File("trammel", Block("Static", 0x0063), Block("Static", 0x0063)));

        var first = await service.DecorateAsync(_progress);
        var second = await service.DecorateAsync(_progress);

        Assert.Equal(new DecorationResult(1, 1, 0, 1), first);
        Assert.Equal(new DecorationResult(0, 2, 0, 1), second);
        Assert.Single(_items.Items);
    }

    [Fact]
    public async Task DecorateAsync_AnotherGraphicOnTheSameSpot_IsPlaced()
    {
        await Service(File("trammel", Block("Static", 0x0063), Block("Static", 0x0064))).DecorateAsync(_progress);

        Assert.Equal(2, _items.Items.Count);
    }

    [Fact]
    public async Task DecorateAsync_LinksAdjacentDoorsOfTheSameType_Only()
    {
        var file = File(
            "trammel",
            Block("MetalDoor", 0x0675, new Point3D(100, 100, 0), new Point3D(101, 100, 0), new Point3D(110, 100, 0)),
            Block("DarkWoodDoor", 0x06A5, new Point3D(110, 101, 0))
        );

        await Service(file).DecorateAsync(_progress);

        var byX = _items.Items.ToDictionary(item => (item.X!.Value, item.Y!.Value));
        var left = byX[(100, 100)];
        var right = byX[(101, 100)];
        Assert.Equal((long)right.Id.Value, left.Props!["door.link"]);
        Assert.Equal((long)left.Id.Value, right.Props!["door.link"]);
        Assert.False(byX[(110, 100)].Props!.ContainsKey("door.link"));
        Assert.False(byX[(110, 101)].Props!.ContainsKey("door.link"));
        Assert.Contains(_factory.Saved.SelectMany(batch => batch), item => item.Props?.ContainsKey("door.link") == true);
    }

    [Fact]
    public async Task DecorateAsync_AnOpenDoor_CountsAsThereOnItsClosedSpot()
    {
        var service = Service(File("trammel", Block("MetalDoor", 0x0675, props: new() { ["facing"] = "west_cw" })));
        await service.DecorateAsync(_progress);
        var door = Assert.Single(_items.Items);

        // What door.lua leaves on an open door: the next graphic, moved aside, the closed spot kept.
        _sectors.RemoveItem(door);
        door.ItemId = 0x0676;
        door.PlaceOnGround(MapType.Trammel, new Point3D(1499, 1601, 10));
        door.Props!["door.open"] = true;
        door.Props["door.x"] = 1500L;
        door.Props["door.y"] = 1600L;
        door.Props["door.z"] = 10L;
        _sectors.AddItem(door);

        var second = await service.DecorateAsync(_progress);

        Assert.Equal(new DecorationResult(0, 1, 0, 1), second);
        Assert.Single(_items.Items);
    }

    [Fact]
    public async Task DecorateAsync_TheSavedItemsEnterTheWorld_EvenWhenSavingTheLinksFails()
    {
        _factory.FailingSave = 2;

        var result = await Service(
                         File("trammel", Block("MetalDoor", 0x0675, new Point3D(100, 100, 0), new Point3D(101, 100, 0)))
                     )
                     .DecorateAsync(_progress);

        Assert.Equal(new DecorationResult(2, 0, 0, 1), result);
        Assert.Equal(2, _items.Items.Count);
        Assert.All(_items.Items, door => Assert.True(door.Props!.ContainsKey("door.link")));
    }

    [Fact]
    public async Task DecorateAsync_WhileAnotherRuns_IsRefused()
    {
        var loader = new StubDecorationsLoader(File("trammel", Block("Static", 0x0063))) { Gate = new() };
        var service = new DecorationService(loader, _factory, _items, _sectors, _view, new StubGameLoop());

        var first = service.DecorateAsync(_progress);
        Assert.True(service.IsRunning);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DecorateAsync(_progress));
        loader.Gate.SetResult();

        Assert.Equal(1, (await first).Placed);
        Assert.False(service.IsRunning);
        Assert.Equal(0, (await service.DecorateAsync(_progress)).Placed);
    }

    [Fact]
    public async Task DecorateAsync_ReportsEveryFile_AndReturnsTheTotals()
    {
        var result = await Service(
                         File("trammel", Block("Static", 0x0063)),
                         File("felucca", Block("Static", 0x0063), Block("Teleporter", 0x1BC3))
                     )
                     .DecorateAsync(_progress);

        Assert.Equal(new DecorationResult(2, 0, 1, 2), result);
        Assert.Equal(["trammel", "felucca"], _progress.Reports.Select(report => report.Folder));
    }

    private DecorationService Service(params DecorationFile[] files)
    {
        return new(new StubDecorationsLoader(files), _factory, _items, _sectors, _view, new StubGameLoop());
    }

    private static DecorationFile File(string folder, params DecorationBlock[] blocks)
    {
        var maps = folder == "britannia" ? new[] { MapType.Trammel, MapType.Felucca } :
            folder == "felucca" ? [MapType.Felucca] : [MapType.Trammel];

        return new() { Folder = folder, Name = "town", Maps = maps, Blocks = blocks };
    }

    private static DecorationBlock Block(string type, int? itemId, params Point3D[] locations)
    {
        return Block(type, itemId, null, locations);
    }

    private static DecorationBlock Block(
        string type,
        int? itemId,
        Dictionary<string, object>? props,
        params Point3D[] locations
    )
    {
        return new()
        {
            Type = type,
            ItemId = itemId,
            Props = props ?? [],
            Locations = locations.Length > 0 ? locations : [new Point3D(1500, 1600, 10)]
        };
    }
}
