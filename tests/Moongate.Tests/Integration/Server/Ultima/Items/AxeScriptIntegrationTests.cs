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
    private readonly StubItemSerialPool _serials = new();
    private readonly ItemService _items;
    private readonly HarvestService _harvest;

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "hatchet", ItemId = new Serial(0x0F43), ScriptId = "axe" },
            new ItemTemplate { Id = "0x1be0_log", ItemId = new Serial(0x1BE0), Stackable = true }
        )
    );

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _pole = new()
        { Id = new Serial(0x40000002), TemplateId = "hatchet", ItemId = 0x0F43, Amount = 1 };

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private MobileEntity _aria = null!;
    private Point3D _water;
    private readonly HashSet<string> _fired = [];

    public AxeScriptIntegrationTests()
    {
        _items = TestItems.Create(_sectors);
        // The least of the area: 2 cuts, back 20 minutes after the first.
        _harvest = new(
            new StubDataLoaderService().With(
                new HarvestResource
                {
                    Id = "wood", Area = 4, AmountMin = 2, AmountMax = 4, RespawnMinMinutes = 20, RespawnMaxMinutes = 30
                }
            ),
            _time,
            new ScriptedRandom()
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
        _water = new Point3D(_aria.Location.X + 2, _aria.Location.Y, 20);

        _backpack.Equip(new Serial((uint)Aria), LayerType.Backpack);
        _pole.Equip(new Serial((uint)Aria), LayerType.TwoHanded);
        _items.Add([_backpack, _pole]);

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

        Use(_water);

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
        Assert.Equal(1, _harvest.Amount("wood", MapType.Trammel, _water.X, _water.Y));
    }

    [Theory]
    [InlineData(0.0, 0.9)]
    [InlineData(0.3, 2.5)]
    [InlineData(0.9, 4.1)]
    public void Chopping_TakesOneToThreeSwings(double roll, double seconds)
    {
        Rolls(roll);

        Use(_water);

        Assert.Equal(seconds, _timers.Timers.Max(timer => timer.Interval.TotalSeconds), 3);
    }

    [Fact]
    public void Chopping_TwiceInARow_StacksTheLogs()
    {
        Rolls(0.0, 0.0);

        Use(_water);
        Fire(0.9);
        Use(_water);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal(20, Assert.Single(Caught()).Amount);
        Assert.Equal(0, _harvest.Amount("wood", MapType.Trammel, _water.X, _water.Y));
    }

    [Fact]
    public void Chopping_WithTheAxeInTheBackpack_NeedsItEquipped()
    {
        _items.MoveToContainer(_pole, _backpack.Id, new Point2D(44, 65));

        Use(_water);

        Assert.Empty(_errors);
        Assert.Equal([NotEquipped], Told());
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public void Chopping_WhatIsNoTree_CannotUseAnAxeOnThat()
    {
        // A rock, the bare land, and a mobile.
        Use(_water, 0x1773);
        Use(_water, 0);
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

        Use(_water);

        Assert.NotEmpty(_timers.Timers);
    }

    [Fact]
    public void Chopping_WhileChopping_DoesNothing_AndIsFreeAgainAfterTheResult()
    {
        Rolls(0.0, 0.0);
        Use(_water);
        Use(_water);

        Assert.Equal([UseOnWhat], Told());
        Assert.Single(_timers.Timers);

        Fire(0.9);
        Use(_water);

        Assert.Empty(_errors);
        Assert.Equal(UseOnWhat, Told()[^1]);
    }

    [Fact]
    public void Chopping_WhereNoWoodIsLeft_SaysSoAtOnce_AndItComesBack()
    {
        _harvest.TryTake("wood", MapType.Trammel, _water.X, _water.Y);
        _harvest.TryTake("wood", MapType.Trammel, _water.X, _water.Y);

        Use(_water);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, NoWood], Told());
        Assert.Empty(_timers.Timers);

        _time.Advance(TimeSpan.FromMinutes(20));
        Use(_water);

        Assert.NotEmpty(_timers.Timers);
    }

    [Fact]
    public void Chopping_ATryThatFails_GivesNoWood_AndTakesNoneFromThePlace()
    {
        Skill(0);
        Rolls(0.0);

        Use(_water);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, Failed], Told());
        Assert.Empty(Caught());
        Assert.Equal(2, _harvest.Amount("wood", MapType.Trammel, _water.X, _water.Y));
    }

    [Fact]
    public void Chopping_LogsTheBackpackCannotTake_SaysSo_AndTheWoodIsGoneFromThePlace()
    {
        Rolls(0.0);
        _serials.Serials.Clear();

        Use(_water);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat, NoRoom], Told());
        Assert.Equal(1, _harvest.Amount("wood", MapType.Trammel, _water.X, _water.Y));
    }

    [Fact]
    public void Chopping_ThePlayerWalksAway_OrPutsTheAxeAway_GetsNothing()
    {
        Rolls(0.0, 0.0);
        Use(_water);
        _aria.Location = new Point3D(_water.X - 5, _aria.Location.Y, _aria.Location.Z);
        Fire(0.9);

        Assert.Equal([UseOnWhat, TooFar], Told());

        _aria.Location = new Point3D(_water.X - 2, _aria.Location.Y, _aria.Location.Z);
        Use(_water);
        _items.MoveToContainer(_pole, _backpack.Id, new Point2D(44, 65));
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Empty(Caught().Where(item => item.Id != _pole.Id));
        Assert.Equal(2, _harvest.Amount("wood", MapType.Trammel, _water.X, _water.Y));
    }

    [Fact]
    public void Chopping_ThePlayerLeavesTheWorldMeanwhile_GetsNothing_WithoutAnError()
    {
        Rolls(0.5);
        Use(_water);
        _fixture.Mobiles.LeaveWorld(_aria.Id);
        Fire(0.9);
        Fire(1.6);
        Fire(2.5);

        Assert.Empty(_errors);
        Assert.Equal([UseOnWhat], Told());
        Assert.Equal(2, _harvest.Amount("wood", MapType.Trammel, _water.X, _water.Y));
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
        _itemScripts.Run(_pole, "set_rolls", rolls.Cast<object?>().ToArray());
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

    private void Run()
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(_pole, "on_use", Aria);

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

    // What lies in the backpack beside the pole.
    private List<ItemEntity> Caught()
    {
        return _items.GetContents(_backpack.Id).Where(item => item.Id != _pole.Id).ToList();
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
