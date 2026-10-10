using System.Numerics;
using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Containers;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Commands;

/// <summary>
///     <c>.add_spell</c> and <c>.add_reagents</c> over the shipped <c>spells.toml</c> and item templates, with the real
///     spellbook service: the whole of Magery in a book, and the eight classic reagents in a backpack.
/// </summary>
public sealed class SpellStaffCommandsIntegrationTests : IAsyncLifetime
{
    private readonly StubTargetService _targets = new();
    private readonly StubItemHandlingService _handling = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingSpeechService _speech = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private ItemService _items = null!;
    private SpellCatalogService _catalog = null!;
    private ItemTemplateService _templates = null!;
    private SpellbookService _books = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        _items = TestItems.Create(_fixture.Sectors);
        var directories = new DirectoriesConfig(Path.Combine(RepositoryRoot(), "moongate_root"), ["data", "templates"]);
        var shipped = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        _templates = new(new StubDataLoaderService().With(shipped));
        var spells = (await new SpellsLoader(directories, new StubDataLoaderService().With(shipped)).LoadDataAsync())
            .Entities.ToArray();
        _catalog = new(new StubDataLoaderService().With(spells), _templates);
        _books = new(_items, _templates, _catalog, _fixture.Sender, _speech, _handling, _fixture.Sessions);

        foreach (var template in shipped.Where(template => template.ItemId.Value > 0))
        {
            _handling.Templates[template.Id] = (int)template.ItemId.Value;
        }
    }

    [Fact]
    public async Task AddSpellAll_FillsAnEmptyBookWithTheSixtyFourSpells()
    {
        var book = new ItemEntity { Id = new Serial(0x40000010), TemplateId = "spellbook", ItemId = 0x0EFA, Amount = 1 };
        _items.Add([book]);
        _targets.Result = TargetResult.ForObject(book.Id);
        var context = Context("add_spell", "all");

        await new AddSpellCommand(_targets, _catalog, _books, _items, _fixture.Mobiles, _fixture.Network.Loop)
            .ExecuteAsync(context);

        Assert.Equal(64, BitOperations.PopCount(_books.GetSpells(book)));
        Assert.Equal(ulong.MaxValue, _books.GetSpells(book));
        Assert.Equal(
            "Spells added to the spellbook: 64 new, 64 in the book now.",
            Assert.Single(context.Output).Text
        );
    }

    [Fact]
    public async Task AddReagentsAll_GivesTheEightClassicReagents()
    {
        var context = Context("add_reagents", "all");

        await new AddReagentsCommand(
                _catalog,
                _templates,
                _handling,
                _items,
                _view,
                _fixture.Mobiles,
                _fixture.Network.Loop
            )
            .ExecuteAsync(context);

        Assert.Equal(
            [
                "0x0f7a_black_pearl", "0x0f7b_blood_moss", "0x0f84_garlic", "0x0f85_ginseng", "0x0f86_mandrake_root",
                "0x0f88_nightshade", "0x0f8c_sulfurous_ash", "0x0f8d_spider_silk"
            ],
            _handling.Given.Select(item => item.TemplateId).Order()
        );
        Assert.All(_handling.Given, item => Assert.Equal(20, item.Amount));
    }

    [Fact]
    public async Task AddSpellAll_OnTheGameMastersOwnBook_OpensItOnceWithOneContentUpdate()
    {
        var book = CarriedBook(_session.CharacterId, 0x40000011);
        _targets.Result = TargetResult.ForObject(book.Id);

        await RunAddSpellAsync(book, "all");

        Assert.Single(_fixture.Sender.Sent.OfType<DisplayContainerPacket>());
        var content = Assert.Single(_fixture.Sender.Sent.OfType<ContainerContentPacket>());
        Assert.Equal(64, content.Items.Count);
    }

    [Fact]
    public async Task AddSpellAll_OnAnotherPlayersBook_NeverPopsTheBookOpenOnTheirScreen()
    {
        var other = await _fixture.AddAsync(3);
        var book = CarriedBook(other.CharacterId, 0x40000012);

        await RunAddSpellAsync(book, "circle", "2");

        Assert.Empty(_fixture.Sender.Sent.OfType<DisplayContainerPacket>());
        var content = Assert.Single(_fixture.Sender.Sent.OfType<ContainerContentPacket>());
        Assert.Equal(8, content.Items.Count);
    }

    [Fact]
    public async Task AddReagentsAll_ThroughTheRealGive_FillsTheBackpackByWeight_AndLeavesTheRestAtTheFeet()
    {
        var real = RealHandling(out var weight, out var fatigue);
        var backpack = CarriedBackpack(_session.CharacterId);
        // 1000 of each is 100 stones a stack: four fit the 400 stones of a backpack, the other four lie at the feet.
        var context = Context("add_reagents", "all", "1000");
        await new AddReagentsCommand(
                _catalog,
                _templates,
                real,
                _items,
                _view,
                _fixture.Mobiles,
                _fixture.Network.Loop,
                weight: weight,
                fatigue: fatigue
            )
            .ExecuteAsync(context);

        Assert.Equal(4, _items.GetContents(backpack.Id).Count);
        Assert.Equal(400, weight.Of(backpack) - 1);
        Assert.Equal(4, _view.Calls.Count);
        Assert.Equal(2, context.Output.Count);
        Assert.Equal([(true, false)], fatigue.Loads.Select(load => (load.Mobile.Id == _session.CharacterId, load.Warn)));
    }

    [Fact]
    public async Task AddReagentsAll_ThroughTheRealGive_PutsWhatTheItemCountRefusesAtTheFeet()
    {
        var real = RealHandling(out var weight, out var fatigue, room: false);
        var backpack = CarriedBackpack(_session.CharacterId);
        var pearl = new ItemEntity
        {
            Id = new Serial(0x40000020), TemplateId = "0x0f7a_black_pearl", ItemId = 3962, Amount = 5
        };
        pearl.PutInContainer(backpack.Id, new Point2D(10, 10));
        _items.Add([pearl]);
        var context = Context("add_reagents", "all", "1");

        await new AddReagentsCommand(
                _catalog,
                _templates,
                real,
                _items,
                _view,
                _fixture.Mobiles,
                _fixture.Network.Loop,
                weight: weight,
                fatigue: fatigue
            )
            .ExecuteAsync(context);

        // The pearls join their stack though the backpack has no room for another item; the other seven lie at the feet.
        Assert.Equal(6, pearl.Amount);
        Assert.Equal([pearl], _items.GetContents(backpack.Id));
        Assert.Equal(7, _view.Calls.Count);
    }

    private async Task RunAddSpellAsync(ItemEntity book, params string[] arguments)
    {
        _targets.Result = TargetResult.ForObject(book.Id);
        await new AddSpellCommand(_targets, _catalog, _books, _items, _fixture.Mobiles, _fixture.Network.Loop)
            .ExecuteAsync(Context("add_spell", arguments));
    }

    private ItemEntity CarriedBackpack(Serial owner)
    {
        var backpack = new ItemEntity
            { Id = new Serial(0x40000001 + owner.Value), TemplateId = "0x0e75_backpack", ItemId = 0x0E75, Amount = 1 };
        backpack.Equip(owner, LayerType.Backpack);
        _items.Add([backpack]);

        return backpack;
    }

    private ItemEntity CarriedBook(Serial owner, uint serial)
    {
        var backpack = CarriedBackpack(owner);
        var book = new ItemEntity { Id = new Serial(serial), TemplateId = "spellbook", ItemId = 0x0EFA, Amount = 1 };
        book.PutInContainer(backpack.Id, new Point2D(1, 1));
        _items.Add([book]);

        return book;
    }

    private ItemHandlingService RealHandling(out WeightService weight, out RecordingFatigueService fatigue, bool room = true)
    {
        var tiles = new FakeTileDataService().Item(0x0E75, TileFlagType.Container, 0);
        var serials = new StubItemSerialPool();

        for (uint index = 0; index < 64; index++)
        {
            serials.Serials.Enqueue(new Serial(0x40001000 + index));
        }

        weight = new(_items, _templates, tiles);
        fatigue = new();

        return new(
            _items,
            _fixture.Sessions,
            _fixture.Sender,
            _view,
            TestTooltips.Create(_items, _fixture.Mobiles),
            new FakeItemFactoryService(_templates, tiles),
            serials,
            capacity: new StubContainerCapacityService { HasRoomResult = room },
            templates: _templates,
            tiles: tiles
        );
    }

    private CommandContext Context(string name, params string[] arguments)
    {
        return new("." + name, name, arguments, CommandSourceType.InGame, _session);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Moongate.slnx not found above the test output.");
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
