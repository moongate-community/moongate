using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ItemSpawnServiceTests
{
    private static readonly Point3D Spot = new(1500, 1600, 5);

    private readonly StubDataLoaderService _data = new StubDataLoaderService()
        .With(
            new ItemTemplate { Id = "chest", ItemId = new Serial(0x0E41), Movable = false, Loot = ["gems", "gems"], Gold = DiceSpec.FromValue(99) },
            new ItemTemplate { Id = "rich_chest", ItemId = new Serial(0x0E41), Gold = DiceSpec.FromValue(69_999) },
            new ItemTemplate { Id = "box", ItemId = new Serial(0x09A8) },
            new ItemTemplate { Id = "0x0eed_gold_coin", ItemId = new Serial(0x0EED), Stackable = true },
            new ItemTemplate { Id = "ruby", ItemId = new Serial(0x0F13), Stackable = true }
        )
        .With(new LootTemplate { Id = "gems", Entries = [new() { ItemId = "ruby", Amount = RangeValueSpec<int>.FromValue(3) }] })
        .With(new ContainerContent { Default = true, Gump = 0x3C, Bounds = new(44, 65, 142, 94) });

    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingItemScriptService _scripts = new();
    private readonly StubGameLoop _loop = new();
    private readonly FakeItemFactoryService _factory;
    private readonly ItemService _items;
    private readonly ItemSpawnService _service;

    public ItemSpawnServiceTests()
    {
        var templates = new ItemTemplateService(_data);
        var tiles = new FakeTileDataService();
        _factory = new(templates, tiles);
        _items = TestItems.Create(TestSectors.Create());
        _service = new(
            _factory,
            templates,
            new LootService(_data, _factory, templates, tiles),
            new ContainerLayoutService(_data),
            _items,
            _view,
            _loop,
            new ItemsConfig(),
            _scripts
        );
    }

    [Fact]
    public async Task SpawnAsync_PutsTheItemOnTheGround_WithItsProps_AndShowsIt()
    {
        var box = await _service.SpawnAsync("box", MapType.Felucca, Spot, new Dictionary<string, object?> { ["spawn.region"] = "r1" });

        Assert.True(_items.TryGet(box.Id, out var live));
        Assert.Same(box, live);
        Assert.Equal(
            (ItemLocationType.Ground, (MapType?)MapType.Felucca, new Point3D(1500, 1600, 5)),
            (box.Location, box.Map, new Point3D(box.X!.Value, box.Y!.Value, box.Z!.Value))
        );
        Assert.True(box.TryGetProp<string>("spawn.region", out var region));
        Assert.Equal("r1", region);
        Assert.Empty(_items.GetContents(box.Id));
        Assert.Equal([$"Appeared {box.Id.Value}"], _view.Calls);
    }

    [Fact]
    public async Task SpawnAsync_AContainerWithLootAndGold_IsFilled()
    {
        var chest = await _service.SpawnAsync("chest", MapType.Felucca, Spot);

        var contents = _items.GetContents(chest.Id);
        Assert.Equal(["0x0eed_gold_coin", "ruby", "ruby"], contents.Select(item => item.TemplateId));
        Assert.Equal([99, 3, 3], contents.Select(item => item.Amount));
        Assert.All(contents, item => Assert.Equal((ItemLocationType.Container, chest.Id), (item.Location, item.ContainerId)));
        // Each on a slot of its own, for the grid of the Enhanced Client.
        Assert.Equal(3, contents.Select(item => item.GridIndex).Distinct().Count());
        // The contents are not shown: the client asks when the chest is opened.
        Assert.Equal([$"Appeared {chest.Id.Value}"], _view.Calls);
    }

    [Fact]
    public async Task SpawnAsync_GoldAboveOnePile_IsSplit()
    {
        var chest = await _service.SpawnAsync("rich_chest", MapType.Felucca, Spot);

        Assert.Equal([65_535, 4_464], _items.GetContents(chest.Id).Select(item => item.Amount));
    }

    [Fact]
    public async Task SpawnAsync_SavesTheContainerBeforeItsContents()
    {
        var chest = await _service.SpawnAsync("chest", MapType.Felucca, Spot);

        Assert.Equal(2, _factory.Saved.Count);
        Assert.Equal([chest], _factory.Saved[0]);
        Assert.Equal(3, _factory.Saved[1].Count);
    }

    [Fact]
    public async Task SpawnAsync_AnItemWithoutContents_IsSavedOnce()
    {
        await _service.SpawnAsync("box", MapType.Felucca, Spot);

        Assert.Single(_factory.Saved);
    }

    [Fact]
    public async Task SpawnAsync_QueuesOnCreate_ForTheItemAndItsContents()
    {
        _scripts.Scripted.UnionWith(["chest", "ruby"]);

        await _service.SpawnAsync("chest", MapType.Felucca, Spot);

        Assert.Equal(3, _scripts.Queued.Count);
        Assert.All(_scripts.Queued, call => Assert.Contains("on_create", call));
    }

    [Fact]
    public async Task SpawnAsync_AnUnknownTemplate_Throws_AndNothingEntersTheWorld()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.SpawnAsync("nothing", MapType.Felucca, Spot));

        Assert.Empty(_items.Items);
        Assert.Empty(_factory.Saved);
    }

    [Fact]
    public async Task SpawnAsync_ASaveThatFails_Throws_AndNothingEntersTheWorld()
    {
        _factory.FailingSave = 2;

        await Assert.ThrowsAsync<IOException>(() => _service.SpawnAsync("chest", MapType.Felucca, Spot));

        Assert.Empty(_items.Items);
        Assert.Empty(_view.Calls);
    }
}
