using Moongate.Server.Ultima.Handlers.Items.Internal;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using DryIoc;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Tests.TestSupport.Ultima.Effects;
using Moongate.Tests.TestSupport.Ultima.Death;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Types.Effects;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.HuePicking;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Core.Data.Config;
using Moongate.Tests.TestSupport.Randomness;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Server.Ultima.Data.Mobiles;

using Moongate.Server.Ultima.Data.Harvest;
using Moongate.Tests.Support.Timing;
using Moongate.Tests.TestSupport.Ultima.Movement;

namespace Moongate.Tests.Integration.Server.Ultima.Items;

/// <summary>
///     The shipped <c>scripts/items/axe.lua</c>, with the real Lua engine and the real harvest service.
/// </summary>
public sealed class AxeScriptIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;
    private const int Tree = 0x0CDD;

    private const int UseOnWhat = 1010018;
    private const int NotEquipped = 500487;
    private const int NotATree = 500489;
    private const int TooFar = 500446;
    private const int NoWood = 500493;
    private const int Failed = 500495;
    private const int NoRoom = 500497;
    private const int Chopped = 500498;
    private const int InBackpack = 1062334;
    private const int NotOnThat = 500494;
    private const int Kindled = 500491;
    private const int StrangeWood = 1072652;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly StubTargetService _targets = new();
    private readonly ScriptedRandom _random = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly RecordingEffectService _effects = new();
    private readonly StubMovementService _movement = new();
    private readonly ManualTimeProvider _time = new();

    // What the place draws when it fills: its cuts above the least, then its kind among the 1000 of the weights.
    private readonly ScriptedRandom _place = new();
    private readonly StubItemSerialPool _serials = new();
    private readonly ItemService _items;
    private readonly HarvestService _harvest;

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "hatchet", ItemId = new Serial(0x0F43), ScriptId = "axe" },
            new ItemTemplate { Id = "0x1be0_log", ItemId = new Serial(0x1BE0), Stackable = true },
            new ItemTemplate { Id = "0x1bdd_log", ItemId = new Serial(0x1BDE), Stackable = true },
            new ItemTemplate { Id = "0x1bd7_board", ItemId = new Serial(0x1BD7), Stackable = true },
            new ItemTemplate { Id = "0x0de1_kindling", ItemId = new Serial(0x0DE1), Stackable = true },
            new ItemTemplate { Id = "oak_log", ItemId = new Serial(0x1BE0), Stackable = true },
            new ItemTemplate { Id = "oak_board", ItemId = new Serial(0x1BD7), Stackable = true },
            new ItemTemplate { Id = "ash_log", ItemId = new Serial(0x1BE0), Stackable = true },
            new ItemTemplate { Id = "ash_board", ItemId = new Serial(0x1BD7), Stackable = true },
            new ItemTemplate { Id = "yew_log", ItemId = new Serial(0x1BE0), Stackable = true },
            new ItemTemplate { Id = "yew_board", ItemId = new Serial(0x1BD7), Stackable = true },
            new ItemTemplate { Id = "heartwood_log", ItemId = new Serial(0x1BE0), Stackable = true },
            new ItemTemplate { Id = "heartwood_board", ItemId = new Serial(0x1BD7), Stackable = true },
            new ItemTemplate { Id = "bloodwood_log", ItemId = new Serial(0x1BE0), Stackable = true },
            new ItemTemplate { Id = "bloodwood_board", ItemId = new Serial(0x1BD7), Stackable = true },
            new ItemTemplate { Id = "frostwood_log", ItemId = new Serial(0x1BE0), Stackable = true },
            new ItemTemplate { Id = "frostwood_board", ItemId = new Serial(0x1BD7), Stackable = true },
            new ItemTemplate { Id = "bark_fragment", ItemId = new Serial(0x318F), Stackable = true },
            new ItemTemplate { Id = "luminescent_fungi", ItemId = new Serial(0x318F), Stackable = true },
            new ItemTemplate { Id = "switch", ItemId = new Serial(0x318F), Stackable = true },
            new ItemTemplate { Id = "parasitic_plant", ItemId = new Serial(0x318F), Stackable = true },
            new ItemTemplate { Id = "brilliant_amber", ItemId = new Serial(0x318F), Stackable = true },
            new ItemTemplate { Id = "dagger", ItemId = new Serial(0x0F51), ScriptId = "blade" }
        )
    );

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _axe = new()
        { Id = new Serial(0x40000002), TemplateId = "hatchet", ItemId = 0x0F43, Amount = 1 };

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private MobileEntity _aria = null!;
    private Point3D _tree;
    private readonly HashSet<string> _fired = [];
    private uint _next = 0x40000050;

    public AxeScriptIntegrationTests()
    {
        _items = TestItems.Create(_sectors);
        // The least of the area: 2 cuts, back 20 minutes after the first.
        _harvest = new(
            new StubDataLoaderService().With(
                new HarvestResource
                {
                    Id = "wood", Area = 4, AmountMin = 2, AmountMax = 4, RespawnMinMinutes = 20, RespawnMaxMinutes = 30,
                    // The weights of the shipped data/harvest.toml: a place is plain unless the test draws otherwise.
                    Vein =
                    [
                        new() { Id = "plain", Weight = 490 }, new() { Id = "oak", Weight = 300 },
                        new() { Id = "ash", Weight = 100 }, new() { Id = "yew", Weight = 50 },
                        new() { Id = "heartwood", Weight = 30 }, new() { Id = "bloodwood", Weight = 20 },
                        new() { Id = "frostwood", Weight = 10 }
                    ]
                }
            ),
            _time,
            _place
        );
    }

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync((int)Aria);
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Aria), out _aria!));
        _aria.AccountId = new Serial(0x42);

        // A master lumberjack, so the try always passes; the tree is two tiles east, and the axe in the hands.
        Skill(1000);
        _tree = new Point3D(_aria.Location.X + 2, _aria.Location.Y, 20);

        _backpack.Equip(new Serial((uint)Aria), LayerType.Backpack);
        _axe.Equip(new Serial((uint)Aria), LayerType.TwoHanded);
        _items.Add([_backpack, _axe]);

        for (uint serial = 0x40000100; serial < 0x40000110; serial++)
        {
            _serials.Serials.Enqueue(new Serial(serial));
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        // The rolls of the script are the test's: the ones queued, then a high one, which is three swings.
        _scripts.Write(
            "items/axe.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "axe.lua")) +
            """

            function axe.set_rolls(serial, ...)
                local rolls = { ... }
                axe.roll = function() return table.remove(rolls, 1) or 0.999 end
            end
            """
        );
        _scripts.Write("items/blade.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "blade.lua")));
        _scripts.Write("common/trees.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "trees.lua")));
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        var data = new StubDataLoaderService().With(new SkillContent { Id = SkillType.Lumberjacking, GainFactor = 1.0, Delay = 1 });

        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IItemService>(_items);
        _container.RegisterInstance<IItemTemplateService>(_templates);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<IMobileStateService>(_state);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<ITooltipService>(TestTooltips.Create(_items, _fixture.Mobiles));
        _container.RegisterInstance<ITargetService>(_targets);
        _container.RegisterInstance<ISkillService>(new SkillService(_state, data, new SkillsConfig(), _random));
        _container.RegisterInstance<ITeleportService>(
            new TeleportService(
                _fixture.Mobiles,
                _view,
                _fixture.Sessions,
                _fixture.Sender,
                _fixture.Sectors,
                new StubBankService()
            )
        );
        _container.RegisterInstance<IItemFactoryService>(new FakeItemFactoryService(_templates, new FakeTileDataService()));
        _container.RegisterInstance<IItemSerialPool>(_serials);
        _container.RegisterInstance<ITileDataService>(new FakeTileDataService());
        _container.Register<IItemHandlingService, ItemHandlingService>(Reuse.Singleton);
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<ILineOfSightService>(_sight);
        _container.RegisterInstance<IMovementService>(_movement);
        _container.RegisterInstance<IDeathService>(new StubDeathService());
        _container.RegisterInstance<IEffectService>(_effects);
        _container.RegisterInstance<IHarvestService>(_harvest);
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<TargetModule>();
        _container.AddScriptModule<SkillModule>();
        _container.AddScriptModule<WorldModule>();
        _container.AddScriptModule<EffectModule>();
        _container.AddScriptModule<HarvestModule>();
        _container.RegisterDelegate<IScriptEngine>(_ => _engine);
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
        _itemScripts = new(_engine, _templates, _loop, options);
        await _itemScripts.StartAsync();
    }

    [Fact]
    public void Chopping_ATree_SwingsAndGivesTenLogs_WhenTheLastSwingLands()
    {
        // The middle of the five: two swings.
        Rolls(0.5);

        Use(_tree);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat], Told());
        // The first swing at once; the second 1.6 seconds later; the axe is heard 0.9 seconds after each.
        Assert.Equal(["Animated 2 13 5 1"], _view.Calls.Where(call => call.StartsWith("Animated", StringComparison.Ordinal)));
        Assert.Equal([0.9, 1.6, 2.5], _timers.Timers.Select(timer => timer.Interval.TotalSeconds).Order());

        Fire(0.9);
        Assert.Equal([0x13E], _speech.Sounds.Select(sound => sound.Sound));
        Assert.Empty(Caught());

        Fire(1.6);
        Fire(2.5);

        Assert.Empty(_errors);
        Assert.Equal(2, _view.Calls.Count(call => call.StartsWith("Animated", StringComparison.Ordinal)));
        Assert.Equal([UseOnWhat, Chopped], Told());
        var logs = Assert.Single(Caught());
        Assert.Equal((0x1BE0, 10), (logs.ItemId, logs.Amount));
        Assert.Equal(1, _harvest.Amount("wood", MapType.Trammel, _tree.X, _tree.Y));
    }

    [Theory]
    [InlineData(0.0, 0.9)]
    [InlineData(0.3, 2.5)]
    [InlineData(0.9, 4.1)]
    public void Chopping_TakesOneToThreeSwings(double roll, double seconds)
    {
        Rolls(roll);

        Use(_tree);

        Assert.Equal(seconds, _timers.Timers.Max(timer => timer.Interval.TotalSeconds), 3);
    }

    [Theory]
    // The five rolls of the swings: one, two, two, two, three.
    [InlineData(0.1, 1)]
    [InlineData(0.3, 2)]
    [InlineData(0.5, 2)]
    [InlineData(0.7, 2)]
    [InlineData(0.9, 3)]
    public void Chopping_SwingsOnceTwiceOrThrice_TwiceMoreOftenThanNot(double roll, int swings)
    {
        Rolls(roll);

        Use(_tree);

        // A sound for each swing, and a timer for each swing after the first, 1.6 seconds apart.
        var seconds = _timers.Timers.Select(timer => Math.Round(timer.Interval.TotalSeconds, 1)).Order().ToArray();
        var expected = Enumerable.Range(0, swings).Select(swing => Math.Round(swing * 1.6 + 0.9, 1))
            .Concat(Enumerable.Range(1, swings - 1).Select(swing => Math.Round(swing * 1.6, 1)))
            .Order()
            .ToArray();
        Assert.Equal(expected, seconds);
    }

    [Theory]
    // The first and the last graphic of each range of trees, and what lies just outside them.
    [InlineData(0x0CCA, true)]
    [InlineData(0x0CE8, true)]
    [InlineData(0x0CE9, false)]
    [InlineData(0x0CF8, true)]
    [InlineData(0x0D03, true)]
    [InlineData(0x0D41, true)]
    [InlineData(0x0D53, true)]
    [InlineData(0x0D57, true)]
    [InlineData(0x0D69, true)]
    [InlineData(0x0D6E, true)]
    [InlineData(0x0D7F, true)]
    [InlineData(0x0D80, false)]
    [InlineData(0x0D84, true)]
    [InlineData(0x0D90, true)]
    [InlineData(0x0D95, true)]
    [InlineData(0x0D98, false)]
    [InlineData(0x0D9B, true)]
    [InlineData(0x0D9F, true)]
    [InlineData(0x0DA3, true)]
    [InlineData(0x0DA7, true)]
    [InlineData(0x0DAB, true)]
    [InlineData(0x0DAC, false)]
    [InlineData(0x12B5, true)]
    [InlineData(0x12C7, true)]
    [InlineData(0x12C8, false)]
    public void Chopping_KnowsATreeByItsGraphic(int graphic, bool tree)
    {
        Use(_tree, graphic);

        Assert.Empty(_errors);
        Assert.Equal(tree ? [UseOnWhat] : new[] { UseOnWhat, NotATree }, Told());
    }

    [Fact]
    public void Chopping_TheLastCutTakenMeanwhile_GivesNoWood_AtTheLastSwing()
    {
        Rolls(0.0);
        Use(_tree);
        _harvest.TryTake("wood", MapType.Trammel, _tree.X, _tree.Y);
        _harvest.TryTake("wood", MapType.Trammel, _tree.X, _tree.Y);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, NoWood], Told());
        Assert.Empty(Caught());
    }

    [Fact]
    public void Chopping_ThePlayerDiesAtTheFirstSwing_SwingsNoMore_AndGetsNothing()
    {
        Rolls(0.9);
        Use(_tree);
        _aria.Body = 0x0192;
        Fire(0.9);
        Fire(1.6);
        Fire(2.5);
        Fire(3.2);
        Fire(4.1);

        Assert.Empty(_errors);
        // The swing played at once is the only one, and the axe is not heard.
        Assert.Single(_view.Calls, call => call.StartsWith("Animated", StringComparison.Ordinal));
        Assert.Empty(_speech.Sounds);
        Assert.Equal([UseOnWhat], Told());
        Assert.Equal(2, _harvest.Amount("wood", MapType.Trammel, _tree.X, _tree.Y));

        // And free to chop again once alive.
        _aria.Body = 0x0190;
        Use(_tree);
        Assert.Equal(UseOnWhat, Told()[^1]);
    }

    [Fact]
    public void Chopping_TwiceInARow_StacksTheLogs()
    {
        // One swing each time; between the two the roll of a master's find, which finds nothing.
        Rolls(0.0, 0.999, 0.0);

        Use(_tree);
        Fire(0.9);
        Use(_tree);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal(20, Assert.Single(Caught()).Amount);
        Assert.Equal(0, _harvest.Amount("wood", MapType.Trammel, _tree.X, _tree.Y));
    }

    [Theory]
    // Both shapes of a log are sawn, the whole stack, one board for each log.
    [InlineData("0x1be0_log", 0x1BE0, 30)]
    [InlineData("0x1bdd_log", 0x1BDE, 1)]
    public void Sawing_LogsInTheBackpack_TurnsTheWholeStackIntoBoards(string template, int graphic, int amount)
    {
        var logs = Carry(template, graphic, amount);

        Use(logs.Id);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat], Told());
        var boards = Assert.Single(Caught());
        Assert.Equal(("0x1bd7_board", amount), (boards.TemplateId, boards.Amount));
        Assert.Equal([0x13E], _speech.Sounds.Select(sound => sound.Sound));
        // No tree, no swing, no skill tried.
        Assert.Empty(_timers.Timers);
        Assert.Equal(0, _random.Rolls);
    }

    [Fact]
    public void Sawing_BoardsJoinTheBoardsAlreadyThere()
    {
        Carry("0x1bd7_board", 0x1BD7, 5);

        Use(Carry("0x1be0_log", 0x1BE0, 10).Id);

        Assert.Equal(15, Assert.Single(Caught()).Amount);
    }

    [Fact]
    public async Task Sawing_LogsOnTheGroundOrOnACursor_NeedsThemInTheBackpack_AndGivesNoBoards()
    {
        var ground = Carry("0x1be0_log", 0x1BE0, 10);
        _items.PlaceOnGround(ground, MapType.Trammel, new Point3D(_aria.Location.X + 1, _aria.Location.Y, 0));
        Use(ground.Id);

        // Lifted, the logs still count as lying in the backpack and cannot be taken from.
        var held = Carry("0x1be0_log", 0x1BE0, 10);
        var holder = _fixture.Sessions.GetAll().First(session => session.CharacterId == _aria.Id);
        await _fixture.Network.ExecuteOnLoopAsync(() => holder.Set(ItemSessionKeys.Held, new HeldItem(held.Id)));
        Use(held.Id);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, InBackpack, UseOnWhat, InBackpack], Told());
        Assert.Equal((10, 10), (ground.Amount, held.Amount));
        Assert.DoesNotContain(Caught(), item => item.TemplateId == "0x1bd7_board");
    }

    [Fact]
    public void Sawing_LogsInTheBankBox_NeedsThemInTheBackpack_ButLogsInABagAreSawn()
    {
        // The bank box is worn too, so its logs are the player's: a target reaches them with the bank closed.
        var bank = new ItemEntity { Id = new Serial(0x40000091), TemplateId = "backpack", ItemId = 0x0E7C, Amount = 1 };
        bank.Equip(new Serial((uint)Aria), LayerType.Bank);
        var bag = new ItemEntity { Id = new Serial(0x40000092), TemplateId = "backpack", ItemId = 0x0E76, Amount = 1 };
        bag.PutInContainer(_backpack.Id, new Point2D(20, 20));
        _items.Add([bank, bag]);
        var banked = Carry("0x1be0_log", 0x1BE0, 10);
        _items.MoveToContainer(banked, bank.Id, new Point2D(10, 10));
        var bagged = Carry("0x1be0_log", 0x1BE0, 7);
        _items.MoveToContainer(bagged, bag.Id, new Point2D(10, 10));

        Use(banked.Id);
        Use(bagged.Id);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, InBackpack, UseOnWhat], Told());
        Assert.Equal(10, banked.Amount);
        Assert.Equal(7, Assert.Single(Caught(), item => item.TemplateId == "0x1bd7_board").Amount);
    }

    [Fact]
    public void Sawing_WhatIsNoLog_CannotUseAnAxeOnThat()
    {
        Use(Carry("0x1bd7_board", 0x1BD7, 5).Id);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, NotATree], Told());
        Assert.Equal(5, Assert.Single(Caught()).Amount);
    }

    [Fact]
    public void ABlade_OnATree_HacksOneKindlingOff_AtTheCostOfACutOfItsWood()
    {
        var dagger = Carry("dagger", 0x0F51, 1);

        Use(dagger, _tree);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, Kindled], Told());
        var kindling = Assert.Single(Caught(), item => item.TemplateId == "0x0de1_kindling");
        Assert.Equal(1, kindling.Amount);
        Assert.Empty(_timers.Timers);
        Assert.Equal(1, _harvest.Amount("wood", MapType.Trammel, _tree.X, _tree.Y));
    }

    [Fact]
    public void ABlade_GetsAsMuchKindlingAsThePlaceHasCuts_ThenNoneUntilTheWoodIsBack()
    {
        var dagger = Carry("dagger", 0x0F51, 1);

        Use(dagger, _tree);
        Use(dagger, _tree);
        Use(dagger, _tree);

        // Kindling a vendor buys must not come for ever from one tree.
        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, Kindled, UseOnWhat, Kindled, UseOnWhat, NoWood], Told());
        Assert.Equal(2, Assert.Single(Caught(), item => item.TemplateId == "0x0de1_kindling").Amount);
    }

    [Fact]
    public void ABlade_OnWhatIsNoTree_TooFar_OrWhereNoWoodIsLeft_GivesNothing()
    {
        var dagger = Carry("dagger", 0x0F51, 1);

        Use(dagger, _tree, 0x1773);
        Use(dagger, new Point3D(_aria.Location.X + 3, _aria.Location.Y, 20));
        _harvest.TryTake("wood", MapType.Trammel, _tree.X, _tree.Y);
        _harvest.TryTake("wood", MapType.Trammel, _tree.X, _tree.Y);
        Use(dagger, _tree);
        _targets.Result = TargetResult.ForObject(_aria.Id);
        Run(dagger);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, NotOnThat, UseOnWhat, TooFar, UseOnWhat, NoWood, UseOnWhat, NotOnThat], Told());
        Assert.DoesNotContain(Caught(), item => item.TemplateId == "0x0de1_kindling");
    }

    [Fact]
    public void ABlade_LyingOnTheGround_GetsNoCursor()
    {
        var dagger = Carry("dagger", 0x0F51, 1);
        _items.PlaceOnGround(dagger, MapType.Trammel, new Point3D(_aria.Location.X + 1, _aria.Location.Y, 0));

        Use(dagger, _tree);

        Assert.Empty(_errors);
        Assert.Empty(Told());
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public void Chopping_WithTheAxeInTheBackpack_NeedsItEquipped()
    {
        _items.MoveToContainer(_axe, _backpack.Id, new Point2D(44, 65));

        Use(_tree);

        Assert.Empty(_errors);
        Assert.Equal([NotEquipped], Told());
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public void Chopping_WhatIsNoTree_CannotUseAnAxeOnThat()
    {
        // A rock, the bare land, and a mobile.
        Use(_tree, 0x1773);
        Use(_tree, 0);
        Use(_aria.Id);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, NotATree, UseOnWhat, NotATree, UseOnWhat, NotATree], Told());
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Chopping_ATreeThreeTilesAway_IsTooFar_AndTwoIsNot()
    {
        Use(new Point3D(_aria.Location.X + 3, _aria.Location.Y, 20));

        Assert.Equal([UseOnWhat, TooFar], Told());
        Assert.Empty(_timers.Timers);

        Use(_tree);

        Assert.NotEmpty(_timers.Timers);
    }

    [Fact]
    public void Chopping_WhileChopping_DoesNothing_AndIsFreeAgainAfterTheResult()
    {
        Rolls(0.0, 0.0);
        Use(_tree);
        Use(_tree);

        Assert.Equal([UseOnWhat], Told());
        Assert.Single(_timers.Timers);

        Fire(0.9);
        Use(_tree);

        Assert.Empty(_errors);
        Assert.Equal(UseOnWhat, Told()[^1]);
    }

    [Fact]
    public void Chopping_WhereNoWoodIsLeft_SaysSoAtOnce_AndItComesBack()
    {
        _harvest.TryTake("wood", MapType.Trammel, _tree.X, _tree.Y);
        _harvest.TryTake("wood", MapType.Trammel, _tree.X, _tree.Y);

        Use(_tree);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, NoWood], Told());
        Assert.Empty(_timers.Timers);

        _time.Advance(TimeSpan.FromMinutes(20));
        Use(_tree);

        Assert.NotEmpty(_timers.Timers);
    }

    [Fact]
    public void Chopping_ATryThatFails_GivesNoWood_AndTakesNoneFromThePlace()
    {
        Skill(0);
        Rolls(0.0);

        Use(_tree);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, Failed], Told());
        Assert.Empty(Caught());
        Assert.Equal(2, _harvest.Amount("wood", MapType.Trammel, _tree.X, _tree.Y));
    }

    [Fact]
    public void Chopping_LogsTheBackpackCannotTake_SaysSo_AndTheWoodIsGoneFromThePlace()
    {
        Rolls(0.0);
        _serials.Serials.Clear();

        Use(_tree);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, NoRoom], Told());
        Assert.Equal(1, _harvest.Amount("wood", MapType.Trammel, _tree.X, _tree.Y));
    }

    [Fact]
    public void Chopping_ThePlayerWalksAway_OrPutsTheAxeAway_GetsNothing()
    {
        Rolls(0.0, 0.0);
        Use(_tree);
        _aria.Location = new Point3D(_tree.X - 5, _aria.Location.Y, _aria.Location.Z);
        Fire(0.9);

        Assert.Equal([UseOnWhat, TooFar], Told());

        _aria.Location = new Point3D(_tree.X - 2, _aria.Location.Y, _aria.Location.Z);
        Use(_tree);
        _items.MoveToContainer(_axe, _backpack.Id, new Point2D(44, 65));
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Empty(Caught());
        Assert.Equal(2, _harvest.Amount("wood", MapType.Trammel, _tree.X, _tree.Y));
    }

    [Fact]
    public void Chopping_ThePlayerLeavesTheWorldMeanwhile_GetsNothing_WithoutAnError()
    {
        Rolls(0.5);
        Use(_tree);
        _fixture.Mobiles.LeaveWorld(_aria.Id);
        Fire(0.9);
        Fire(1.6);
        Fire(2.5);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat], Told());
        Assert.Equal(2, _harvest.Amount("wood", MapType.Trammel, _tree.X, _tree.Y));
    }

    [Theory]
    [InlineData(490, "oak_log", 1072541)]
    [InlineData(790, "ash_log", 1072542)]
    [InlineData(890, "yew_log", 1072543)]
    [InlineData(940, "heartwood_log", 1072544)]
    [InlineData(970, "bloodwood_log", 1072545)]
    [InlineData(990, "frostwood_log", 1072546)]
    public void Chopping_InAPlaceOfAKind_AMasterGetsTheLogsOfTheKind(int draw, string logs, int text)
    {
        _place.Integers(0, draw);
        // One swing; then the kind is kept (half of the times it is not); the try of the skill passes.
        Rolls(0.0, 0.5);
        _random.Doubles(0.0);

        Use(_tree);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, text], Told());
        Assert.Equal((logs, 10), Assert.Single(Caught().Select(item => (item.TemplateId, item.Amount))));
        Assert.Equal(1, _harvest.Amount("wood", MapType.Trammel, _tree.X, _tree.Y));
    }

    [Fact]
    public void Chopping_InAPlaceOfAKind_HalfOfTheCutsGivePlainLogs()
    {
        // Oak.
        _place.Integers(0, 490);
        Rolls(0.0, 0.49);

        Use(_tree);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, Chopped], Told());
        Assert.Equal(("0x1be0_log", 10), Assert.Single(Caught().Select(item => (item.TemplateId, item.Amount))));
    }

    [Theory]
    // Oak asks for 65, yew for 95, frostwood for 100.
    [InlineData(490, 649, "0x1be0_log")]
    [InlineData(490, 650, "oak_log")]
    [InlineData(890, 949, "0x1be0_log")]
    [InlineData(890, 950, "yew_log")]
    [InlineData(990, 999, "0x1be0_log")]
    public void Chopping_AKind_AsksForItsSkill_OneWhoLacksItGetsPlainLogs(int draw, int tenths, string logs)
    {
        _place.Integers(0, draw);
        Skill(tenths);
        Rolls(0.0, 0.9);
        _random.Doubles(0.0);

        Use(_tree);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal(logs, Assert.Single(Caught()).TemplateId);
    }

    [Fact]
    public void Chopping_AKind_IsTriedBetweenItsOwnBounds_HarderThanPlainWood()
    {
        // Oak at 65 is tried between 25 and 105: one chance in two, where plain wood would give 65 in 100.
        _place.Integers(0, 490);
        Skill(650);
        Rolls(0.0, 0.9);
        _random.Doubles(0.55);

        Use(_tree);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, Failed], Told());
        Assert.Empty(Caught());
    }

    [Theory]
    [InlineData(0.0, "bark_fragment", 1072548)]
    [InlineData(0.099, "bark_fragment", 1072548)]
    [InlineData(0.1, "luminescent_fungi", 1072550)]
    [InlineData(0.13, "switch", 1072547)]
    [InlineData(0.15, "parasitic_plant", 1072549)]
    [InlineData(0.1605, "brilliant_amber", 1072551)]
    public void Chopping_AMaster_SometimesFindsSomethingRareWithTheLogs(double roll, string find, int text)
    {
        Rolls(0.0, roll);

        Use(_tree);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, Chopped, text], Told());
        Assert.Equal(
            [("0x1be0_log", 10), (find, 1)],
            Caught().Select(item => (item.TemplateId, item.Amount)).OrderBy(item => item.TemplateId != "0x1be0_log")
        );
    }

    [Theory]
    // Most cuts find nothing; one below 100 never does.
    [InlineData(1000, 0.161)]
    [InlineData(999, 0.0)]
    public void Chopping_FindsNothingRare_MostOfTheTimes_AndNeverBelowAMaster(int tenths, double roll)
    {
        Skill(tenths);
        Rolls(0.0, roll);
        _random.Doubles(0.0);

        Use(_tree);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, Chopped], Told());
        Assert.Equal("0x1be0_log", Assert.Single(Caught()).TemplateId);
    }

    [Theory]
    [InlineData("oak", 650)]
    [InlineData("ash", 800)]
    [InlineData("yew", 950)]
    [InlineData("heartwood", 1000)]
    [InlineData("bloodwood", 1000)]
    [InlineData("frostwood", 1000)]
    public void Sawing_LogsOfAKind_GivesItsBoards_ToOneWhoHasTheSkillOfTheKind(string kind, int tenths)
    {
        Skill(tenths);
        var logs = Carry(kind + "_log", 0x1BE0, 12);

        Use(logs.Id);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat], Told());
        Assert.Equal((kind + "_board", 12), Assert.Single(Caught().Select(item => (item.TemplateId, item.Amount))));

        // One tenth of a point less, and the wood is too strange to work.
        Skill(tenths - 1);
        var more = Carry(kind + "_log", 0x1BE0, 5);

        Use(more.Id);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, UseOnWhat, StrangeWood], Told());
        Assert.Equal(5, more.Amount);
        Assert.Equal(12, Assert.Single(Caught(), item => item.TemplateId == kind + "_board").Amount);
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        await _fixture.DisposeAsync();
        _scripts.Dispose();
    }

    private void Skill(int tenths)
    {
        // The skill service reads the mobile, the mobile module the state service: both hold the same.
        _aria.Skills.Clear();
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Lumberjacking, Base = tenths });
        _state.Skills.Clear();
        _state.Skills.Add(new MobileSkill { Skill = SkillType.Lumberjacking, Base = tenths });
    }

    private void Rolls(params double[] rolls)
    {
        _itemScripts.Run(_axe, "set_rolls", rolls.Cast<object?>().ToArray());
    }

    private void Use(Point3D place, int graphic = Tree)
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, place, graphic);
        Run();
    }

    private void Use(Serial picked)
    {
        _targets.Result = TargetResult.ForObject(picked);
        Run();
    }

    // Double clicks another tool and picks a place.
    private void Use(ItemEntity tool, Point3D place, int graphic = Tree)
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, place, graphic);
        Run(tool);
    }

    // An item in the backpack.
    private ItemEntity Carry(string template, int graphic, int amount)
    {
        var item = new ItemEntity { Id = new Serial(_next++), TemplateId = template, ItemId = graphic, Amount = amount };
        item.PutInContainer(_backpack.Id, new Point2D(70, 70));
        _items.Add([item]);

        return item;
    }

    private void Run()
    {
        Run(_axe);
    }

    private void Run(ItemEntity tool)
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(tool, "on_use", Aria);

        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }

        _loop.DeferTryPost = false;
    }

    // Fires the oldest timer of that many seconds that has not fired yet.
    private void Fire(double seconds)
    {
        var timer = _timers.Timers.First(timer => !_fired.Contains(timer.Id) &&
                                                  Math.Abs(timer.Interval.TotalSeconds - seconds) < 0.001
        );
        _fired.Add(timer.Id);
        _timers.Fire(timer.Id);
    }

    private List<int> Told()
    {
        return _speech.ToldClilocs.Where(told => told.Player == _aria).Select(told => told.Cliloc).ToList();
    }

    // What lies in the backpack, the tools aside.
    private List<ItemEntity> Caught()
    {
        return _items.GetContents(_backpack.Id).Where(item => item.Id != _axe.Id && item.TemplateId != "dagger").ToList();
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
}
