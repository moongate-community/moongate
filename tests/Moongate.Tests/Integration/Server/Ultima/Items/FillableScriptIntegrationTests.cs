using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Types.Scripts;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Items;

/// <summary>
///     Runs the script of the town containers shipped in <c>moongate_root</c> (scripts/items/fillable.lua) with the
///     real
///     Lua engine: what a double click puts into a container, and when.
/// </summary>
public sealed class FillableScriptIntegrationTests : IAsyncLifetime
{
    private const long Player = 2;
    private const int Hour = 3600;

    private static readonly Point3D Spot = new(1500, 1600, 0);

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly SettableClock _clock = new();
    private readonly StubItemSerialPool _serials = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly FakeTileDataService _tiles = new FakeTileDataService().Item(0x0E3C, TileFlagType.Container, 0);

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate
                { Id = "decoration_fillable", ItemId = new Serial(0x0E3C), Movable = false, ScriptId = "fillable" },
            new ItemTemplate { Id = "book", ItemId = new Serial(0x0FEF) },
            new ItemTemplate { Id = "bread", ItemId = new Serial(0x103B) },
            new ItemTemplate { Id = "hammer", ItemId = new Serial(0x13E3) }
        )
    );

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private ItemService _items = null!;
    private uint _nextItem = 0x40000010;
    private uint _nextNpc = 0x100;
    private uint _nextLoot = 0x40001000;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(Player);
        _items = TestItems.Create(_fixture.Sectors);

        Refill();

        _scripts.Write(
            "items/fillable.lua",
            await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "moongate_root", "scripts", "items", "fillable.lua"))
        );
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        var factory = new FakeItemFactoryService(_templates, _tiles);
        // Every roll of a table gives one item, so the counts of the script show.
        var loot = new StubDataLoaderService().With(
            new LootTemplate { Id = "fillable_library", Entries = [new() { ItemId = "book" }] },
            new LootTemplate { Id = "fillable_baker", Entries = [new() { ItemId = "bread" }] },
            new LootTemplate { Id = "fillable_blacksmith", Entries = [new() { ItemId = "hammer" }] }
        );
        var view = new RecordingWorldViewService();

        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<TimeProvider>(_clock);
        _container.RegisterInstance<IItemService>(_items);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<IWorldViewService>(view);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISpeechService>(new RecordingSpeechService());
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<ITooltipService>(TestTooltips.Create(_items, _fixture.Mobiles));
        _container.RegisterInstance<ITeleportService>(new RecordingTeleportService());
        _container.RegisterInstance<IItemFactoryService>(factory);
        _container.RegisterInstance<IItemSerialPool>(_serials);
        _container.RegisterInstance<ITileDataService>(_tiles);
        _container.RegisterInstance<IItemTemplateService>(_templates);
        _container.RegisterInstance<ILootService>(new LootService(loot, factory, _templates, _tiles));
        _container.Register<IItemHandlingService, ItemHandlingService>(Reuse.Singleton);
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<WorldModule>();
        _container.AddScriptModule<MobileModule>();
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );

        _engine = new(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
        await _engine.StartAsync();
        _itemScripts = new ItemScriptService(_engine, _templates, _loop, options);
        await _itemScripts.StartAsync();
    }

    [Fact]
    public void ABookcaseOpenedEmpty_GetsOneToFiveBooks_AndWaitsAnHourToAnHourAndAHalf()
    {
        var bookcase = Container("library");

        Open(bookcase);

        var books = _items.GetContents(bookcase.Id);
        Assert.InRange(books.Count, 1, 5);
        Assert.All(books, book => Assert.Equal("book", book.TemplateId));
        Assert.InRange(NextFill(bookcase), Now() + Hour, Now() + Hour + Hour / 2);
        Assert.Empty(_errors);
    }

    [Fact]
    public void OpenedAgainBeforeItsTime_NothingIsAdded()
    {
        var bookcase = Container("library");
        Open(bookcase);
        var before = _items.GetContents(bookcase.Id).Count;
        Empty(bookcase);

        _clock.Advance(TimeSpan.FromMinutes(59));
        Open(bookcase);

        Assert.Empty(_items.GetContents(bookcase.Id));
        Assert.InRange(before, 1, 5);
        Assert.Empty(_errors);
    }

    [Fact]
    public void OpenedAfterItsTime_ItFillsAgain()
    {
        var bookcase = Container("library");
        Open(bookcase);
        Empty(bookcase);

        _clock.Advance(TimeSpan.FromMinutes(91));
        Open(bookcase);

        Assert.InRange(_items.GetContents(bookcase.Id).Count, 1, 5);
        Assert.Empty(_errors);
    }

    // The pool of serials can be empty for a moment: the container must not wait an hour with nothing inside.
    [Fact]
    public void AFillThatCouldAddNothing_IsTriedAgainAtTheNextOpening()
    {
        var bookcase = Container("library");
        var serials = _serials.Serials.ToList();
        _serials.Serials.Clear();

        Open(bookcase);

        Assert.Empty(_items.GetContents(bookcase.Id));
        Assert.False(bookcase.TryGetProp<long>("fill.next", out _));

        serials.ForEach(_serials.Serials.Enqueue);
        Open(bookcase);

        Assert.InRange(_items.GetContents(bookcase.Id).Count, 1, 5);
        Assert.Empty(_errors);
    }

    [Fact]
    public async Task AWaitress_MakesATavern()
    {
        var crate = Container(null);
        await AddNpcAsync("f_waitress", 1501, 1600);

        Open(crate);

        Assert.True(crate.TryGetProp<string>("content_type", out var kind));
        Assert.Equal("tavern", kind);
    }

    [Fact]
    public void ABookcaseWithFiveBooks_GetsNoMore()
    {
        var bookcase = Container("library");
        Put(bookcase, 5);

        Open(bookcase);

        Assert.Equal(5, _items.GetContents(bookcase.Id).Count);
        Assert.Empty(_errors);
    }

    [Fact]
    public void ACrateWithMoreThanTwoItems_GetsNoMore_AndIsLookedAtAgainAtTheNextOpening()
    {
        var crate = Container("baker");
        Put(crate, 3);

        Open(crate);

        Assert.Equal(3, _items.GetContents(crate.Id).Count);
        Assert.False(crate.TryGetProp<long>("fill.next", out _));
        Assert.Empty(_errors);
    }

    // Up to (1 + 2 - items) * 2, as ModernUO: none to six for an empty one, none to two for one with two items. Over
    // many crates the share is never passed and is not always nothing.
    [Theory]
    [InlineData(0, 6)]
    [InlineData(2, 2)]
    public void ACrateWithFewItems_GetsUpToItsShare(int held, int most)
    {
        var added = new List<int>();

        for (var index = 0; index < 20; index++)
        {
            var crate = Container("baker");
            Put(crate, held);

            Open(crate);

            added.Add(_items.GetContents(crate.Id).Count(item => item.TemplateId == "bread"));
            Assert.InRange(NextFill(crate), Now() + Hour, Now() + Hour + Hour / 2);
            Refill();
        }

        Assert.InRange(added.Max(), 1, most);
        Assert.Empty(_errors);
    }

    // As ModernUO, what counts is how many things lie inside, not how many piles.
    [Fact]
    public void ACrateWithOnePileOfThreeThings_IsFullEnough()
    {
        var crate = Container("baker");
        var arrows = new ItemEntity { Id = new Serial(_nextItem++), TemplateId = "hammer", ItemId = 0x13E3, Amount = 3 };
        arrows.PutInContainer(crate.Id, new Point2D(10, 10));
        _items.Add([arrows]);

        Open(crate);

        Assert.Equal([arrows], _items.GetContents(crate.Id));
        Assert.False(crate.TryGetProp<long>("fill.next", out _));
        Assert.Empty(_errors);
    }

    [Theory]
    [InlineData("baker")]
    [InlineData("f_baker")]
    [InlineData("m_baker")]
    public async Task ACrateWithoutAContentType_TakesTheOneOfTheNearestVendor_AndKeepsIt(string template)
    {
        var crate = Container(null);
        await AddNpcAsync("f_blacksmith", 1510, 1600);
        await AddNpcAsync(template, 1503, 1602);
        await AddNpcAsync("orc", 1500, 1601);
        var found = new List<string>();

        // Several crates on the spot: one alone may get nothing.
        for (var index = 0; index < 10; index++)
        {
            var other = Container(null);
            Open(other);
            found.AddRange(_items.GetContents(other.Id).Select(item => item.TemplateId));
        }

        Open(crate);

        Assert.True(crate.TryGetProp<string>("content_type", out var kind));
        Assert.Equal("baker", kind);
        Assert.NotEmpty(found);
        Assert.All(found, template => Assert.Equal("bread", template));
        Assert.Empty(_errors);
    }

    [Fact]
    public async Task ACrateWithNoVendorWithinTwentyTiles_StaysEmpty_AndLooksAgainFiveMinutesLater()
    {
        var crate = Container(null);
        await AddNpcAsync("baker", 1521, 1600);
        await AddNpcAsync("orc", 1500, 1601);

        Open(crate);

        Assert.Empty(_items.GetContents(crate.Id));
        Assert.False(crate.TryGetProp<string>("content_type", out _));
        // Not at every double click: looking for the vendors around costs.
        Assert.Equal(Now() + 300, NextFill(crate));

        // A baker comes to stand by; four minutes later the crate still waits, six minutes later it sees him.
        await AddNpcAsync("baker", 1501, 1600);
        _clock.Advance(TimeSpan.FromMinutes(4));
        Open(crate);
        Assert.False(crate.TryGetProp<string>("content_type", out _));

        _clock.Advance(TimeSpan.FromMinutes(2));
        Open(crate);
        Assert.True(crate.TryGetProp<string>("content_type", out var kind));
        Assert.Equal("baker", kind);
        Assert.Empty(_errors);
    }

    [Fact]
    public void AContentTypeWithoutATable_AddsNothing_AndRaisesNoError()
    {
        var crate = Container("atlantis");

        Open(crate);

        Assert.Empty(_items.GetContents(crate.Id));
        Assert.Empty(_errors);
    }

    // The container opens after the script: it must not say it handled the double click.
    [Fact]
    public void OnUse_NeverHandlesTheDoubleClick()
    {
        var result = Open(Container("library"));

        Assert.NotEqual(ScriptResultKind.Suspended, result.Kind);
        Assert.False(result.Values is [true, ..]);
    }

    private ScriptResult Open(ItemEntity container)
    {
        return _itemScripts.Run(container, "on_use", Player);
    }

    private ItemEntity Container(string? contentType)
    {
        var container = new ItemEntity
            { Id = new Serial(_nextItem++), TemplateId = "decoration_fillable", ItemId = 0x0E3C, Amount = 1 };

        if (contentType is not null)
        {
            container.SetProp("content_type", contentType);
        }

        _items.Add([container]);
        _items.PlaceOnGround(container, MapType.Trammel, Spot);

        return container;
    }

    private void Put(ItemEntity container, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var item = new ItemEntity { Id = new Serial(_nextItem++), TemplateId = "hammer", ItemId = 0x13E3, Amount = 1 };
            item.PutInContainer(container.Id, new Point2D(10, 10), (byte)index);
            _items.Add([item]);
        }
    }

    // The pool of the test holds 64 serials.
    private void Refill()
    {
        _serials.Serials.Clear();

        for (var serial = 0; serial < 64; serial++)
        {
            _serials.Serials.Enqueue(new Serial(_nextLoot++));
        }
    }

    private void Empty(ItemEntity container)
    {
        _items.Remove(_items.GetContents(container.Id).Select(item => item.Id).ToList());
    }

    private Task AddNpcAsync(string template, int x, int y)
    {
        var npc = new MobileEntity
        {
            Id = new Serial(_nextNpc++), Name = template, TemplateId = template, Map = MapType.Trammel,
            Location = new Point3D(x, y, 0)
        };

        return _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.EnterWorld(npc));
    }

    private long Now()
    {
        return _clock.GetUtcNow().ToUnixTimeSeconds();
    }

    private static long NextFill(ItemEntity container)
    {
        Assert.True(container.TryGetProp<long>("fill.next", out var next));

        return next;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }
}
