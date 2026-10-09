using Moongate.Tests.TestSupport.Ultima.Mounts;
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
///     The shipped <c>scripts/items/pickaxe.lua</c> and <c>scripts/items/ore.lua</c>, with the real Lua engine and the
///     real harvest service.
/// </summary>
public sealed class MiningScriptIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;
    private const int Rock = 220;
    private const int Grass = 3;
    private const int CaveFloor = 0x053B;
    private const int ForgeGraphic = 0x0FB1;

    private const int WhereToDig = 503033;
    private const int NotThere = 501862;
    private const int NotThat = 501863;
    private const int TooFar = 500446;
    private const int MovedAway = 503041;
    private const int NoMetal = 503040;
    private const int Gone = 503042;
    private const int Failed = 503043;
    private const int NoRoom = 1010481;
    private const int Dug = 1007072;
    private const int WhichForge = 501971;
    private const int TooLittle = 501987;
    private const int Smelted = 501988;
    private const int Burnt = 501990;
    private const int OreTooFar = 501976;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly RecordingMountService _mounts = new();
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
            new ItemTemplate { Id = "pick", ItemId = new Serial(0x0E86), ScriptId = "pickaxe" },
            new ItemTemplate { Id = "forge", ItemId = new Serial(ForgeGraphic) },
            new ItemTemplate { Id = "0x19b7_iron_ore", ItemId = new Serial(0x19B7), ScriptId = "ore", Stackable = true },
            new ItemTemplate { Id = "0x19b8_iron_ore", ItemId = new Serial(0x19B8), ScriptId = "ore", Stackable = true },
            new ItemTemplate { Id = "0x19b9_iron_ore", ItemId = new Serial(0x19B9), ScriptId = "ore", Stackable = true },
            new ItemTemplate { Id = "0x19ba_iron_ore", ItemId = new Serial(0x19BA), ScriptId = "ore", Stackable = true },
            new ItemTemplate { Id = "0x1bf2_iron_ingot", ItemId = new Serial(0x1BF2), Stackable = true }
        )
    );

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _pick = new()
        { Id = new Serial(0x40000002), TemplateId = "pick", ItemId = 0x0E86, Amount = 1 };

    private readonly ItemEntity _forge = new()
        { Id = new Serial(0x40000003), TemplateId = "forge", ItemId = ForgeGraphic, Amount = 1 };

    private readonly HashSet<string> _fired = [];
    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private MobileEntity _aria = null!;
    private Point3D _rock;
    private uint _nextPile = 0x40000050;

    public MiningScriptIntegrationTests()
    {
        _items = TestItems.Create(_sectors);
        // The least of the area: 10 ore, back 10 minutes after the first.
        _harvest = new(
            new StubDataLoaderService().With(
                new HarvestResource
                {
                    Id = "ore", Area = 8, AmountMin = 10, AmountMax = 34, RespawnMinMinutes = 10, RespawnMaxMinutes = 20
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

        // A master miner, so every try passes; the rock is two tiles east, the pick in the backpack, a forge beside.
        Skill(1000);
        _rock = new Point3D(_aria.Location.X + 2, _aria.Location.Y, 0);

        _backpack.Equip(new Serial((uint)Aria), LayerType.Backpack);
        _pick.PutInContainer(_backpack.Id, new Point2D(44, 65));
        _items.Add([_backpack, _pick, _forge]);
        _items.PlaceOnGround(_forge, MapType.Trammel, new Point3D(_aria.Location.X, _aria.Location.Y + 2, 0));

        for (uint serial = 0x40000100; serial < 0x40000110; serial++)
        {
            _serials.Serials.Enqueue(new Serial(serial));
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        // The rolls of the pickaxe script are the test's: the ones queued, then a high one, which is a large pile.
        _scripts.Write(
            "items/pickaxe.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "pickaxe.lua")) +
            """

            function pickaxe.set_rolls(serial, ...)
                local rolls = { ... }
                pickaxe.roll = function() return table.remove(rolls, 1) or 0.999 end
            end
            """
        );
        _scripts.Write("items/ore.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "ore.lua")));
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        var data = new StubDataLoaderService().With(new SkillContent { Id = SkillType.Mining, GainFactor = 1.0, Delay = 1 });

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
        _container.RegisterInstance<IMountService>(_mounts);
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
        // Until a test queues its own, every roll of the pickaxe is high: the second sound, a large pile.
        Rolls();
    }

    [Fact]
    public void Digging_OnAMount_IsRefused_WithNoSwingAndNoTimer()
    {
        _mounts.Mounted.Add(_aria.Id);

        Dig(_rock);

        Assert.Empty(_errors);
        Assert.Equal([501864], Told());
        Assert.DoesNotContain(_view.Calls, call => call.StartsWith("Animated", StringComparison.Ordinal));
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Digging_TheRockOfAMountain_SwingsOnce_AndGivesALargePileOfIronOre()
    {
        Dig(_rock);

        Assert.Empty(_errors);
        Assert.Equal([WhereToDig], Told());
        Assert.Equal(["Animated 2 11 5 1"], _view.Calls.Where(call => call.StartsWith("Animated", StringComparison.Ordinal)));
        Assert.Equal(0.9, Assert.Single(_timers.Timers).Interval.TotalSeconds, 3);
        Assert.Empty(Carried());

        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal([WhereToDig, Dug], Told());
        Assert.Equal(0x126, Assert.Single(_speech.Sounds).Sound);
        var pile = Assert.Single(Carried());
        Assert.Equal((0x19B9, 1), (pile.ItemId, pile.Amount));
        Assert.Equal(9, _harvest.Amount("ore", MapType.Trammel, _rock.X, _rock.Y));
    }

    [Theory]
    // The second roll picks the pile: small under an eighth, then the two medium ones, then large.
    [InlineData(0.0, 0x19B7)]
    [InlineData(0.12, 0x19B7)]
    [InlineData(0.13, 0x19B8)]
    [InlineData(0.19, 0x19BA)]
    [InlineData(0.25, 0x19B9)]
    [InlineData(0.9, 0x19B9)]
    public void Digging_GivesASmallAMediumOrALargePile_LargeThreeTimesInFour(double roll, int graphic)
    {
        // The first roll picks the sound.
        Rolls(0.0, roll);

        Dig(_rock);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal(graphic, Assert.Single(Carried()).ItemId);
        Assert.Equal(0x125, Assert.Single(_speech.Sounds).Sound);
    }

    [Theory]
    // The first and the last land of some ranges of rock, and what lies just outside them.
    [InlineData(220, true)]
    [InlineData(231, true)]
    [InlineData(232, false)]
    [InlineData(252, true)]
    [InlineData(263, true)]
    [InlineData(543, true)]
    [InlineData(621, true)]
    [InlineData(622, false)]
    [InlineData(2105, true)]
    [InlineData(0x3F39, true)]
    [InlineData(0x3FCF, true)]
    [InlineData(0x3FD0, false)]
    [InlineData(3, false)]
    public void Digging_KnowsRockByTheLandOfThePlace(int land, bool minable)
    {
        Dig(_rock, 0, land);

        Assert.Empty(_errors);
        Assert.Equal(minable ? [WhereToDig] : new[] { WhereToDig, NotThere }, Told());
    }

    [Fact]
    public void Digging_TheFloorOfACave_IsMinable_WhateverTheLandUnderIt_AndAnyOtherStaticIsNot()
    {
        Dig(_rock, CaveFloor, Grass);

        Assert.Equal([WhereToDig], Told());
        Fire(0.9);

        // A tree standing on rock is no rock.
        Dig(_rock, 0x0CDD, Rock);
        // Nor is an item or someone.
        _targets.Result = TargetResult.ForObject(_aria.Id);
        Run(_pick);

        Assert.Empty(_errors);
        Assert.Equal([WhereToDig, Dug, WhereToDig, NotThere, WhereToDig, NotThat], Told());
    }

    [Fact]
    public void Digging_RockThreeTilesAway_IsTooFar()
    {
        Dig(new Point3D(_aria.Location.X + 3, _aria.Location.Y, 0));

        Assert.Empty(_errors);
        Assert.Equal([WhereToDig, TooFar], Told());
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Digging_WhileDigging_DoesNothing_AndIsFreeAgainAfterTheResult()
    {
        Dig(_rock);
        Dig(_rock);

        Assert.Equal([WhereToDig], Told());
        Assert.Single(_timers.Timers);

        Fire(0.9);
        Dig(_rock);

        Assert.Empty(_errors);
        Assert.Equal(WhereToDig, Told()[^1]);
        // The second pile joined the first.
        Assert.Single(Carried());
    }

    [Fact]
    public void Digging_WhereNoOreIsLeft_SaysSoAtOnce_AndItComesBack()
    {
        Empty();

        Dig(_rock);

        Assert.Empty(_errors);
        Assert.Equal([WhereToDig, NoMetal], Told());
        Assert.Empty(_timers.Timers);

        _time.Advance(TimeSpan.FromMinutes(10));
        Dig(_rock);

        Assert.Single(_timers.Timers);
    }

    [Fact]
    public void Digging_TheLastOreTakenMeanwhile_SomeoneGotThereFirst()
    {
        Dig(_rock);
        Empty();
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal([WhereToDig, Gone], Told());
        Assert.Empty(Carried());
    }

    [Fact]
    public void Digging_ATryThatFails_FindsNoOre_AndTakesNoneFromThePlace()
    {
        Skill(0);

        Dig(_rock);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal([WhereToDig, Failed], Told());
        Assert.Empty(Carried());
        Assert.Equal(10, _harvest.Amount("ore", MapType.Trammel, _rock.X, _rock.Y));
    }

    [Fact]
    public void Digging_OreTheBackpackCannotTake_IsLost_AndGoneFromThePlace()
    {
        _serials.Serials.Clear();

        Dig(_rock);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Equal([WhereToDig, NoRoom], Told());
        Assert.Equal(9, _harvest.Amount("ore", MapType.Trammel, _rock.X, _rock.Y));
    }

    [Fact]
    public void Digging_ThePlayerWalksAway_OrDies_OrLeaves_GetsNothing_WithoutAnError()
    {
        Dig(_rock);
        _aria.Location = new Point3D(_rock.X - 5, _aria.Location.Y, _aria.Location.Z);
        Fire(0.9);
        Assert.Equal([WhereToDig, MovedAway], Told());

        _aria.Location = new Point3D(_rock.X - 2, _aria.Location.Y, _aria.Location.Z);
        Dig(_rock);
        _aria.Body = 0x0192;
        Fire(0.9);
        _aria.Body = 0x0190;

        Dig(_rock);
        _fixture.Mobiles.LeaveWorld(_aria.Id);
        Fire(0.9);

        Assert.Empty(_errors);
        Assert.Empty(Carried());
        Assert.Equal(10, _harvest.Amount("ore", MapType.Trammel, _rock.X, _rock.Y));
    }

    [Fact]
    public void Digging_WithTheToolLeftFarAway_GetsNoCursor()
    {
        _items.PlaceOnGround(_pick, MapType.Trammel, new Point3D(_aria.Location.X + 9, _aria.Location.Y, 0));

        Dig(_rock);

        Assert.Empty(_errors);
        Assert.Empty(Told());
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public void Digging_WithTheToolPutAwayWhileTheCursorIsUp_StartsNothing()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, _rock, 0, Rock);
        _loop.DeferTryPost = true;
        _itemScripts.Run(_pick, "on_use", Aria);
        _items.PlaceOnGround(_pick, MapType.Trammel, new Point3D(_aria.Location.X + 9, _aria.Location.Y, 0));

        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }

        _loop.DeferTryPost = false;

        Assert.Empty(_errors);
        Assert.Equal([WhereToDig], Told());
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Digging_ThePickIsNotHeardByWhoWalkedAway()
    {
        Dig(_rock);
        _aria.Location = new Point3D(_rock.X - 5, _aria.Location.Y, _aria.Location.Z);
        Fire(0.9);

        Assert.Empty(_speech.Sounds);
    }

    [Theory]
    // A large pile gives two ingots an ore, a medium one an ingot, a small one an ingot every two with the odd one left.
    [InlineData("0x19b9_iron_ore", 5, 10, 0)]
    [InlineData("0x19b8_iron_ore", 3, 3, 0)]
    [InlineData("0x19ba_iron_ore", 4, 4, 0)]
    [InlineData("0x19b7_iron_ore", 5, 2, 1)]
    [InlineData("0x19b7_iron_ore", 2, 1, 0)]
    public void Smelting_APileAtAForge_TurnsItAllIntoIngots_ByItsSize(string template, int ore, int ingots, int left)
    {
        var pile = Pile(template, ore);

        Smelt(pile, TargetResult.ForObject(_forge.Id));

        Assert.Empty(_errors);
        Assert.Equal([WhichForge, Smelted], Told());
        Assert.Equal(ingots, Assert.Single(Carried(), item => item.TemplateId == "0x1bf2_iron_ingot").Amount);
        Assert.Equal(left, Carried().Where(item => item.Id == pile.Id).Sum(item => item.Amount));
    }

    [Fact]
    public void Smelting_AtAForgeThatIsPartOfTheMap_WorksToo_AndTheIngotsStack()
    {
        var place = new Point3D(_aria.Location.X + 1, _aria.Location.Y, 0);

        Smelt(Pile("0x19b9_iron_ore", 1), TargetResult.ForLocation(MapType.Trammel, place, 0x197A));
        Smelt(Pile("0x19b9_iron_ore", 2), TargetResult.ForLocation(MapType.Trammel, place, 0x19A9));

        Assert.Empty(_errors);
        Assert.Equal(6, Assert.Single(Carried()).Amount);
    }

    [Fact]
    public void Smelting_ASingleSmallOre_IsTooLittle()
    {
        var pile = Pile("0x19b7_iron_ore", 1);

        Smelt(pile, TargetResult.ForObject(_forge.Id));

        Assert.Empty(_errors);
        Assert.Equal([WhichForge, TooLittle], Told());
        Assert.Equal(1, pile.Amount);
    }

    [Fact]
    public void Smelting_OnWhatIsNoForge_OrAForgeTooFar_SmeltsNothing()
    {
        var pile = Pile("0x19b9_iron_ore", 5);

        Smelt(pile, TargetResult.ForObject(_pick.Id));
        Smelt(pile, TargetResult.ForLocation(MapType.Trammel, _rock, 0x0CDD));
        Smelt(pile, TargetResult.ForLocation(MapType.Trammel, _rock, 0));
        _items.PlaceOnGround(_forge, MapType.Trammel, new Point3D(_aria.Location.X, _aria.Location.Y + 3, 0));
        Smelt(pile, TargetResult.ForObject(_forge.Id));
        Smelt(pile, TargetResult.ForLocation(MapType.Trammel, new Point3D(_aria.Location.X + 3, _aria.Location.Y, 0), ForgeGraphic));

        Assert.Empty(_errors);
        Assert.Equal(["That is not a forge.", "That is not a forge.", "That is not a forge."], _speech.Told.Select(told => told.Text));
        Assert.Equal([WhichForge, WhichForge, WhichForge, WhichForge, TooFar, WhichForge, TooFar], Told());
        Assert.Equal(5, pile.Amount);
    }

    [Theory]
    // A smelt that fails burns half the pile away, rounded down.
    [InlineData(5, 2)]
    [InlineData(4, 2)]
    [InlineData(2, 1)]
    public void Smelting_ThatFails_LeavesHalfThePile(int ore, int left)
    {
        Skill(0);
        var pile = Pile("0x19b9_iron_ore", ore);

        Smelt(pile, TargetResult.ForObject(_forge.Id));

        Assert.Empty(_errors);
        Assert.Equal([WhichForge, Burnt], Told());
        Assert.Equal(left, pile.Amount);
        Assert.DoesNotContain(Carried(), item => item.TemplateId == "0x1bf2_iron_ingot");
    }

    [Theory]
    // A single ore gets smaller instead: a large one medium, a medium one small. It is a pile of the smaller kind.
    [InlineData("0x19b9_iron_ore", "0x19b8_iron_ore", 0x19B8)]
    [InlineData("0x19b8_iron_ore", "0x19b7_iron_ore", 0x19B7)]
    [InlineData("0x19ba_iron_ore", "0x19b7_iron_ore", 0x19B7)]
    public void Smelting_ASingleOreThatFails_GetsSmaller(string template, string smaller, int graphic)
    {
        Skill(0);
        var pile = Pile(template, 1);

        Smelt(pile, TargetResult.ForObject(_forge.Id));

        Assert.Empty(_errors);
        Assert.Equal([WhichForge, Burnt], Told());
        var left = Assert.Single(Carried());
        Assert.Equal((smaller, graphic, 1), (left.TemplateId, left.ItemId, left.Amount));
    }

    [Fact]
    public void Smelting_ASmallPileThatFails_LosesHalfToo()
    {
        Skill(0);
        var pile = Pile("0x19b7_iron_ore", 3);

        Smelt(pile, TargetResult.ForObject(_forge.Id));

        Assert.Empty(_errors);
        Assert.Equal((0x19B7, 1), (pile.ItemId, pile.Amount));
    }

    [Fact]
    public async Task Smelting_APileLiftedOntoACursor_SmeltsNothing_AndTriesNoSkill()
    {
        // Lifted, the pile still counts as lying in the backpack and cannot be taken from: ingots must not come of it.
        var pile = Pile("0x19b9_iron_ore", 100);
        var holder = _fixture.Sessions.GetAll().First(session => session.CharacterId == _aria.Id);
        _targets.Result = TargetResult.ForObject(_forge.Id);
        _loop.DeferTryPost = true;
        _itemScripts.Run(pile, "on_use", Aria);
        await _fixture.Network.ExecuteOnLoopAsync(() => holder.Set(ItemSessionKeys.Held, new HeldItem(pile.Id)));

        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }

        _loop.DeferTryPost = false;

        Assert.Empty(_errors);
        Assert.Equal([WhichForge, OreTooFar], Told());
        Assert.Equal(100, pile.Amount);
        Assert.DoesNotContain(Carried(), item => item.TemplateId == "0x1bf2_iron_ingot");
        Assert.Equal(0, _random.Rolls);

        // Held when it is double clicked: no cursor at all.
        Run(pile);
        Assert.Equal([WhichForge, OreTooFar, OreTooFar], Told());
    }

    [Fact]
    public void Smelting_WithNoRoomForTheIngots_LosesTheMetal_TheOreIsGoneAllTheSame()
    {
        var pile = Pile("0x19b9_iron_ore", 5);
        _serials.Serials.Clear();

        Smelt(pile, TargetResult.ForObject(_forge.Id));

        Assert.Empty(_errors);
        Assert.Equal([WhichForge], Told());
        Assert.Equal("You have no room in your backpack for the ingots: the metal is lost.", _speech.Told[^1].Text);
        Assert.Empty(Carried());
    }

    [Fact]
    public void Smelting_MoreIngotsThanAStackHolds_GivesSeveralStacks()
    {
        var pile = Pile("0x19b9_iron_ore", 40_000);

        Smelt(pile, TargetResult.ForObject(_forge.Id));

        Assert.Empty(_errors);
        Assert.Equal([60_000, 20_000], Carried().Select(item => item.Amount).OrderDescending());
    }

    [Fact]
    public void Smelting_APileLyingBesideThePlayer_Works_AndAPileInAChestOnTheGroundGetsNoCursor()
    {
        var pile = Pile("0x19b8_iron_ore", 4);
        _items.PlaceOnGround(pile, MapType.Trammel, new Point3D(_aria.Location.X + 1, _aria.Location.Y, 0));

        Smelt(pile, TargetResult.ForObject(_forge.Id));

        Assert.Empty(_errors);
        Assert.Equal([WhichForge, Smelted], Told());
        Assert.Equal(4, Assert.Single(Carried()).Amount);

        var chest = new ItemEntity { Id = new Serial(0x40000090), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        _items.Add([chest]);
        _items.PlaceOnGround(chest, MapType.Trammel, new Point3D(_aria.Location.X + 1, _aria.Location.Y, 0));
        var inside = Pile("0x19b8_iron_ore", 4);
        _items.MoveToContainer(inside, chest.Id, new Point2D(10, 10));
        var requests = _targets.Requests;

        Smelt(inside, TargetResult.ForObject(_forge.Id));

        Assert.Equal(OreTooFar, Told()[^1]);
        Assert.Equal(requests, _targets.Requests);
        Assert.Equal(4, inside.Amount);
    }

    [Fact]
    public void Smelting_APileNoLongerThere_SmeltsNothing()
    {
        var pile = Pile("0x19b9_iron_ore", 5);
        _targets.Result = TargetResult.ForObject(_forge.Id);
        _items.PlaceOnGround(pile, MapType.Trammel, new Point3D(_aria.Location.X + 9, _aria.Location.Y, 0));

        Run(pile);

        Assert.Empty(_errors);
        Assert.Equal([OreTooFar], Told());
        Assert.DoesNotContain(Carried(), item => item.TemplateId == "0x1bf2_iron_ingot");
        Assert.Equal(5, pile.Amount);
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
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Mining, Base = tenths });
        _state.Skills.Clear();
        _state.Skills.Add(new MobileSkill { Skill = SkillType.Mining, Base = tenths });
    }

    private void Rolls(params double[] rolls)
    {
        _itemScripts.Run(_pick, "set_rolls", rolls.Cast<object?>().ToArray());
    }

    // Double clicks the pick and picks a place: the land by default, rock.
    private void Dig(Point3D place, int graphic = 0, int land = Rock)
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, place, graphic, land);
        Run(_pick);
    }

    // Double clicks a pile and picks what the result says.
    private void Smelt(ItemEntity pile, TargetResult picked)
    {
        _targets.Result = picked;
        Run(pile);
    }

    // A pile of ore in the backpack.
    private ItemEntity Pile(string template, int amount)
    {
        var graphic = Convert.ToInt32(template[2..6], 16);
        var pile = new ItemEntity { Id = new Serial(_nextPile++), TemplateId = template, ItemId = graphic, Amount = amount };
        pile.PutInContainer(_backpack.Id, new Point2D(60, 60));
        _items.Add([pile]);

        return pile;
    }

    // Takes every ore of the place.
    private void Empty()
    {
        while (_harvest.TryTake("ore", MapType.Trammel, _rock.X, _rock.Y))
        {
        }
    }

    private void Run(ItemEntity item)
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(item, "on_use", Aria);

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

    // What lies in the backpack beside the pick.
    private List<ItemEntity> Carried()
    {
        return _items.GetContents(_backpack.Id).Where(item => item.Id != _pick.Id).ToList();
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
