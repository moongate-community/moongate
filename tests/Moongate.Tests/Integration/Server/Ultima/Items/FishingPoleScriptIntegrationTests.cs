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
///     The shipped <c>scripts/items/fishing_pole.lua</c>, with the real Lua engine and the real harvest service.
/// </summary>
public sealed class FishingPoleScriptIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;

    private const int WhatWater = 500974;
    private const int Already = 500972;
    private const int Closer = 500976;
    private const int NotBiting = 503172;
    private const int Nothing = 503171;
    private const int NoRoom = 503176;
    private const int Pulled = 1008124;

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
            new ItemTemplate { Id = "pole", ItemId = new Serial(0x0DC0), ScriptId = "fishing_pole" },
            new ItemTemplate { Id = "0x09cc_fish", ItemId = new Serial(0x09CC), Stackable = true },
            new ItemTemplate { Id = "0x09cd_fish", ItemId = new Serial(0x09CD), Stackable = true },
            new ItemTemplate { Id = "0x09ce_fish", ItemId = new Serial(0x09CE), Stackable = true },
            new ItemTemplate { Id = "0x09cf_fish", ItemId = new Serial(0x09CF), Stackable = true },
            new ItemTemplate { Id = "0x170b_boots", ItemId = new Serial(0x170B) },
            new ItemTemplate { Id = "0x170d_sandals", ItemId = new Serial(0x170D) },
            new ItemTemplate { Id = "0x170f_shoes", ItemId = new Serial(0x170F) },
            new ItemTemplate { Id = "0x1711_thigh_boots", ItemId = new Serial(0x1711) }
        )
    );

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _pole = new()
        { Id = new Serial(0x40000002), TemplateId = "pole", ItemId = 0x0DC0, Amount = 1 };

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private MobileEntity _aria = null!;
    private Point3D _water;

    public FishingPoleScriptIntegrationTests()
    {
        _items = TestItems.Create(_sectors);
        // The least of the area: 5 fish, back 10 minutes after the first catch.
        _harvest = new(
            new StubDataLoaderService().With(
                new HarvestResource
                {
                    Id = "fish", Area = 8, AmountMin = 5, AmountMax = 15, RespawnMinMinutes = 10, RespawnMaxMinutes = 20
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

        // A master fisher, so the try always passes; the water is three tiles east.
        Skill(1000);
        _water = new Point3D(_aria.Location.X + 3, _aria.Location.Y, -5);
        _movement.SwimZ = (x, _) => x >= _water.X ? -5 : null;

        _backpack.Equip(new Serial((uint)Aria), LayerType.Backpack);
        _pole.PutInContainer(_backpack.Id, new Point2D(44, 65));
        _items.Add([_backpack, _pole]);

        for (uint serial = 0x40000100; serial < 0x40000110; serial++)
        {
            _serials.Serials.Enqueue(new Serial(serial));
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        // The rolls of the script are the test's: the ones queued, then a high one, which is "a fish".
        _scripts.Write(
            "items/fishing_pole.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "fishing_pole.lua")) +
            """

            function fishing_pole.set_rolls(serial, ...)
                local rolls = { ... }
                fishing_pole.roll = function() return table.remove(rolls, 1) or 0.999 end
            end
            """
        );
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        var data = new StubDataLoaderService().With(new SkillContent { Id = SkillType.Fishing, GainFactor = 1.0, Delay = 1 });

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
    public void Fishing_InWater_CastsSplashesAndPullsOutAFish_WhenTheEightSecondsAreOver()
    {
        Rolls();
        Use(_water);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater], Told());
        Assert.Equal(["Animated 2 12 5 1"], _view.Calls.Where(call => call.StartsWith("Animated", StringComparison.Ordinal)));
        Assert.Equal([1.5, 8.0], _timers.Timers.Select(timer => timer.Interval.TotalSeconds).Order());

        Fire(1.5);

        Assert.Equal((MapType.Trammel, _water, 0x364), Assert.Single(_speech.PlacedSounds));
        Assert.Equal((MapType.Trammel, _water), (_effects.At[0].Map, _effects.At[0].Location));
        Assert.Empty(Caught());

        Fire(8);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater, Pulled], Told());
        Assert.Equal("fish", _speech.ToldClilocs[^1].Arguments);
        var fish = Assert.Single(Caught());
        Assert.InRange(fish.ItemId, 0x09CC, 0x09CF);
        Assert.Equal(4, _harvest.Amount("fish", MapType.Trammel, _water.X, _water.Y));
    }

    [Fact]
    public void Fishing_WhatIsNotWater_SaysSo_AndStartsNothing()
    {
        Use(new Point3D(_aria.Location.X + 1, _aria.Location.Y, 0));
        Use(_aria.Id);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater, WhatWater], Told());
        Assert.Equal(["You need water to fish in!", "You need water to fish in!"], _speech.Told.Select(told => told.Text));
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Fishing_WaterTooFarOrOutOfSight_NeedsToBeCloser()
    {
        Use(new Point3D(_aria.Location.X + 5, _aria.Location.Y, -5));
        _sight.Allow = false;
        Use(_water);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater, Closer, WhatWater, Closer], Told());
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Fishing_ACursorPutAway_StartsNothing_AndLeavesThePlayerFree()
    {
        _targets.Result = TargetResult.Canceled(TargetCancelType.Canceled);
        Run();
        Use(_water);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater, WhatWater], Told());
        Assert.Equal(2, _timers.Timers.Count);
    }

    [Fact]
    public void Fishing_WhileFishing_IsRefused_AndFreeAgainAfterTheResult()
    {
        Use(_water);
        Use(_water);

        Assert.Equal([WhatWater, Already], Told());
        Assert.Equal(2, _timers.Timers.Count);

        Fire(8);
        Use(_water);

        Assert.Empty(_errors);
        Assert.Equal(WhatWater, Told()[^1]);
    }

    [Fact]
    public void Fishing_WhereNoFishIsLeft_SaysTheyAreNotBiting_AtOnce_AndTheyComeBack()
    {
        for (var take = 0; take < 5; take++)
        {
            _harvest.TryTake("fish", MapType.Trammel, _water.X, _water.Y);
        }

        Use(_water);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater, NotBiting], Told());
        Assert.Empty(_timers.Timers);

        _time.Advance(TimeSpan.FromMinutes(10));
        Use(_water);

        Assert.Equal(2, _timers.Timers.Count);
    }

    [Fact]
    public void Fishing_TheLastFishTakenMeanwhile_IsNotBiting_AtTheResult()
    {
        Use(_water);

        for (var take = 0; take < 5; take++)
        {
            _harvest.TryTake("fish", MapType.Trammel, _water.X, _water.Y);
        }

        Fire(8);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater, NotBiting], Told());
        Assert.Empty(Caught());
    }

    [Fact]
    public void Fishing_ATryThatFails_CatchesNothing_AndTakesNoFishFromThePlace()
    {
        // No skill at all: the try cannot pass.
        Skill(0);

        Use(_water);
        Fire(8);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater, Nothing], Told());
        Assert.Empty(Caught());
        Assert.Equal(5, _harvest.Amount("fish", MapType.Trammel, _water.X, _water.Y));
    }

    [Fact]
    public void Fishing_TheFootwearRoll_PullsOutAPieceOfFootwear()
    {
        // At 100 the footwear comes under 5/525: the first roll. The second picks which: the third of four.
        Rolls(0.005, 0.6);

        Use(_water);
        Fire(8);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater, Pulled], Told());
        Assert.Equal(0x170F, Assert.Single(Caught()).ItemId);
        Assert.Equal("shoes", _speech.ToldClilocs[^1].Arguments);
        Assert.Equal(4, _harvest.Amount("fish", MapType.Trammel, _water.X, _water.Y));
    }

    [Fact]
    public void Fishing_TheNothingRoll_PullsOutNothing_AndTakesNoFish()
    {
        // No footwear, then under the 25% of nothing at 100.
        Rolls(0.5, 0.2);

        Use(_water);
        Fire(8);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater, Nothing], Told());
        Assert.Empty(Caught());
        Assert.Equal(5, _harvest.Amount("fish", MapType.Trammel, _water.X, _water.Y));
    }

    [Fact]
    public void Fishing_ACatchTheBackpackCannotTake_SaysThereIsNoRoom_AndTheFishIsGoneFromThePlace()
    {
        Rolls();
        // Nothing can be made: as a backpack that takes no more.
        _serials.Serials.Clear();

        Use(_water);
        Fire(8);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater, NoRoom], Told());
        Assert.Empty(Caught());
        // Taken all the same: a full backpack is no way to try the skill for ever on the same fish.
        Assert.Equal(4, _harvest.Amount("fish", MapType.Trammel, _water.X, _water.Y));
    }

    [Fact]
    public void Fishing_WaterExactlyFourTilesAway_IsCloseEnough()
    {
        _aria.Location = new Point3D(_water.X - 4, _aria.Location.Y, _aria.Location.Z);

        Use(_water);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater], Told());
        Assert.Equal(2, _timers.Timers.Count);
    }

    [Fact]
    public void Fishing_ThePoleGivenAwayBeforeTheWaterIsPicked_CastsNothing()
    {
        // The pole leaves the backpack while the cursor is up: it lies far away when the water is picked.
        _items.PlaceOnGround(_pole, MapType.Trammel, new Point3D(_aria.Location.X + 9, _aria.Location.Y, 0));

        Use(_water);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater], Told());
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Fishing_ThePlayerLeavesTheWorldMeanwhile_GetsNothing_WithoutAnError()
    {
        Rolls();
        Use(_water);
        _fixture.Mobiles.LeaveWorld(_aria.Id);
        Fire(1.5);
        Fire(8);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater], Told());
        Assert.Equal(5, _harvest.Amount("fish", MapType.Trammel, _water.X, _water.Y));
    }

    [Theory]
    // At skill 50 the footwear comes under 55/525, about 0.105, and nothing under 150/400 = 0.375.
    [InlineData(0.10, 0.0, 0x170B)]
    [InlineData(0.11, 0.37, 0)]
    [InlineData(0.11, 0.38, 0x09CC)]
    public void Fishing_AtHalfTheSkill_TheChancesFollowTheSkill(double first, double second, int expected)
    {
        Skill(500);
        // The try itself passes: the skill service rolls under one half.
        _random.Doubles(0.1);
        // The last roll picks the first of the list.
        Rolls(first, second, 0.0);

        Use(_water);
        Fire(8);

        Assert.Empty(_errors);
        Assert.Equal(expected == 0 ? [] : new[] { expected }, Caught().Select(item => item.ItemId));
    }

    [Fact]
    public void Fishing_ThePlayerWalksAway_LosesTheCatch()
    {
        Use(_water);
        _aria.Location = new Point3D(_water.X - 5, _aria.Location.Y, _aria.Location.Z);
        Fire(8);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater, Closer], Told());
        Assert.Empty(Caught());
        Assert.Equal(5, _harvest.Amount("fish", MapType.Trammel, _water.X, _water.Y));
    }

    [Fact]
    public void Fishing_ThePlayerDiesMeanwhile_GetsNothing_AndIsNoLongerFishing()
    {
        Use(_water);
        _aria.Body = 0x0192;
        Fire(8);

        Assert.Empty(_errors);
        Assert.Equal([WhatWater], Told());
        Assert.Empty(Caught());

        _aria.Body = 0x0190;
        Use(_water);

        Assert.Equal(WhatWater, Told()[^1]);
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
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Fishing, Base = tenths });
        _state.Skills.Clear();
        _state.Skills.Add(new MobileSkill { Skill = SkillType.Fishing, Base = tenths });
    }

    private void Rolls(params double[] rolls)
    {
        _itemScripts.Run(_pole, "set_rolls", rolls.Cast<object?>().ToArray());
    }

    private void Use(Point3D place)
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, place);
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

    // Fires the timer of that many seconds, the oldest first.
    private void Fire(double seconds)
    {
        _timers.Fire(_timers.Timers.First(timer => Math.Abs(timer.Interval.TotalSeconds - seconds) < 0.001).Id);
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
