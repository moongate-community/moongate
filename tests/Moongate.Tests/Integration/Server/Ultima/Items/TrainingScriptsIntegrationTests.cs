using DryIoc;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Tests.TestSupport.Ultima.Combat;
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

using Moongate.Server.Ultima.Data.Combat;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Server.Ultima.Data.Effects;
namespace Moongate.Tests.Integration.Server.Ultima.Items;

/// <summary>
///     The shipped <c>scripts/items/training_dummy.lua</c> and <c>archery_butte.lua</c>, with the real Lua engine.
/// </summary>
public sealed class TrainingScriptsIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;

    private const int Ranged = 501822;
    private const int DummyTooFar = 501816;
    private const int StillSwinging = 501815;
    private const int TooSkilled = 501828;

    private const int NotRanged = 500593;
    private const int NoArrows = 500594;
    private const int NoBolts = 500595;
    private const int StandInFront = 500596;
    private const int NotLinedUp = 500597;
    private const int ButteTooFar = 500598;
    private const int TooClose = 500599;
    private const int Missed = 500604;
    private const int Gathered = 500592;
    private const int TotalOne = 1062719;

    private static readonly WeaponInfo Bow = new(SkillType.Archery, WeaponType.Bow, true, 9, 41, 25);
    private static readonly WeaponInfo Crossbow = new(SkillType.Archery, WeaponType.Crossbow, true, 9, 41, 25);
    private static readonly WeaponInfo Sword = new(SkillType.Swordsmanship, WeaponType.Sword, false, 5, 33, 35);

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly RecordingCombatService _combat = new();
    private readonly RecordingEffectService _effects = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly ScriptedRandom _random = new();
    private readonly SettableClock _clock = new();
    private readonly ItemService _items;

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "dummy", ItemId = new Serial(0x1070), ScriptId = "training_dummy" },
            new ItemTemplate { Id = "butte", ItemId = new Serial(0x100A), ScriptId = "archery_butte", UseRange = 6 }
        )
    );

    private readonly ItemEntity _dummy = new()
        { Id = new Serial(0x40000010), TemplateId = "dummy", ItemId = 0x1070, Amount = 1 };

    private readonly ItemEntity _butte = new()
        { Id = new Serial(0x40000011), TemplateId = "butte", ItemId = 0x100A, Amount = 1 };

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private MobileEntity _aria = null!;
    private Point3D _spot;

    public TrainingScriptsIntegrationTests()
    {
        _items = TestItems.Create(_sectors);
    }

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync((int)Aria);
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Aria), out _aria!));
        _aria.AccountId = new Serial(0x42);
        _spot = _aria.Location;

        // The dummy beside the player, and the butte five tiles west of it: the player faces it from the east.
        _dummy.PlaceOnGround(_aria.Map, new Point3D(_spot.X - 1, _spot.Y, _spot.Z));
        _butte.PlaceOnGround(_aria.Map, new Point3D(_spot.X - 5, _spot.Y, _spot.Z));
        _items.Add([_dummy, _butte]);

        var data = new StubDataLoaderService().With(
            new SkillContent { Id = SkillType.Wrestling, GainFactor = 1.0, Delay = 1 },
            new SkillContent { Id = SkillType.Swordsmanship, GainFactor = 1.0, Delay = 1 },
            new SkillContent { Id = SkillType.Archery, GainFactor = 1.0, Delay = 1 }
        );
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        var root = Path.Combine(RepositoryRoot(), "moongate_root", "scripts", "items");

        foreach (var script in new[] { "training_dummy.lua", "archery_butte.lua" })
        {
            _scripts.Write($"items/{script}", await File.ReadAllTextAsync(Path.Combine(root, script)));
        }

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
        _container.RegisterInstance<ISectorService>(_sectors);
        _container.RegisterInstance<ITooltipService>(TestTooltips.Create(_items, _fixture.Mobiles));
        _container.RegisterInstance<ICombatService>(_combat);
        _container.RegisterInstance<IEffectService>(_effects);
        _container.RegisterInstance<ISkillService>(new SkillService(_state, data, new SkillsConfig(), _random));
        _container.RegisterInstance<TimeProvider>(_clock);
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<ILineOfSightService>(_sight);
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
        _container.Register<IItemHandlingService, ItemHandlingService>(Reuse.Singleton);
        _container.RegisterScriptEnum<EffectGraphicType>();
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<CombatModule>();
        _container.AddScriptModule<SkillModule>();
        _container.AddScriptModule<EffectModule>();
        _container.AddScriptModule<WorldModule>();
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
    public void ADummy_WithFists_SwingsAtIt_ThenSwingsBackForThreeSeconds()
    {
        Use(_dummy);

        Assert.Empty(_errors);
        Assert.Equal([(_aria, _dummy.GroundLocation!.Value.X, _dummy.GroundLocation.Value.Y)], _combat.Swings);
        Assert.Empty(Told());

        // A quarter of a second on, the dummy shows the swinging graphic with a sound; three seconds on, it is at rest.
        Assert.Equal([TimeSpan.FromSeconds(0.25), TimeSpan.FromSeconds(3)], _timers.Timers.Select(timer => timer.Interval));
        _timers.Fire(_timers.Timers[0].Id);
        Assert.Equal(0x1071, _dummy.ItemId);
        Assert.Contains(_speech.PlacedSounds, placed => placed.Sound is 0x3A4 or 0x3A6 or 0x3A9 or 0x3AE or 0x3B4 or 0x3B6);
        _timers.Fire(_timers.Timers[0].Id);
        Assert.Equal(0x1070, _dummy.ItemId);
    }

    [Fact]
    public void ADummy_StillSwinging_CannotBePracticedOnUntilItStops()
    {
        Use(_dummy);

        Use(_dummy);
        Assert.Equal([StillSwinging], Told());
        Assert.Single(_combat.Swings);

        foreach (var timer in _timers.Timers.ToList())
        {
            _timers.Fire(timer.Id);
        }

        Use(_dummy);
        Assert.Equal(2, _combat.Swings.Count);
    }

    [Fact]
    public void ADummy_TriesTheSkillOfTheWeapon_AndTeachesIt()
    {
        _combat.Weapon = Sword;
        // The try passes, then the roll that learns, then one tenth of a point.
        _random.Doubles(0.1, 0.0).Integers(0);

        Use(_dummy);

        Assert.Empty(_errors);
        Assert.Equal([(_aria, SkillType.Swordsmanship, 1, (int?)null)], _state.SkillsSet);
    }

    [Fact]
    public void ADummy_WithABow_OutOfReach_OrForASkillAtItsMost_SaysSo()
    {
        _combat.Weapon = Bow;
        Use(_dummy);

        _combat.Weapon = null;
        _aria.Location = new Point3D(_spot.X + 4, _spot.Y, _spot.Z);
        Use(_dummy);

        _aria.Location = _spot;
        _state.Skills.Add(new MobileSkill { Skill = SkillType.Wrestling, Base = 250 });
        Use(_dummy);

        Assert.Empty(_errors);
        Assert.Equal([Ranged, DummyTooFar, TooSkilled], Told());
        Assert.Empty(_combat.Swings);
    }

    [Fact]
    public void AButte_AShotThatHits_SpendsAnArrow_ShootsIt_AndLeavesItStuck()
    {
        _combat.Weapon = Bow;
        _random.Doubles(0.1);

        Use(_butte);

        Assert.Empty(_errors);
        Assert.Equal([_aria], _combat.Spent);
        Assert.Equal([(_aria, _butte.GroundLocation!.Value.X, _butte.GroundLocation.Value.Y)], _combat.Swings);
        Assert.Equal(0x0F42, Assert.Single(_effects.Moving).Options.Graphic);
        Assert.Equal(1, _butte.GetProp<int>("butte.arrows"));
        Assert.Contains(TotalOne, Labels());
    }

    [Fact]
    public void AButte_AShotThatMisses_ScoresNothing_AndLeavesNothingStuck()
    {
        _combat.Weapon = Bow;
        _random.Doubles(0.9);

        Use(_butte);

        Assert.Empty(_errors);
        Assert.Equal([Missed, TotalOne], Labels());
        Assert.False(_butte.TryGetProp<int>("butte.arrows", out var arrows) && arrows > 0);
    }

    [Theory]
    [InlineData(-1, 0, StandInFront)]
    [InlineData(5, 1, NotLinedUp)]
    [InlineData(7, 0, ButteTooFar)]
    [InlineData(4, 0, TooClose)]
    public void AButte_WhereThePlayerCannotShootFrom_SaysWhy_AndSpendsNothing(int fromTheButte, int rowsOff, int cliloc)
    {
        _combat.Weapon = Bow;
        var butte = _butte.GroundLocation!.Value;
        _aria.Location = new Point3D(butte.X + fromTheButte, butte.Y + rowsOff, butte.Z);

        Use(_butte);

        Assert.Empty(_errors);
        Assert.Equal([cliloc], Told());
        Assert.Empty(_combat.Spent);
    }

    [Fact]
    public void AButte_WithoutARangedWeapon_OrWithoutAmmunition_SaysSo()
    {
        Use(_butte);

        _combat.Weapon = Bow;
        _combat.HasAmmo = false;
        Use(_butte);

        _combat.Weapon = Crossbow;
        Use(_butte);

        Assert.Empty(_errors);
        Assert.Equal([NotRanged, NoArrows, NoBolts], Told());
        Assert.Empty(_combat.Swings);
    }

    [Fact]
    public void AButte_TwoShotsWithinTwoSeconds_AreOneShot()
    {
        _combat.Weapon = Bow;
        _random.Doubles(0.1, 0.1);

        Use(_butte);
        Use(_butte);

        _clock.Advance(TimeSpan.FromSeconds(3));
        Use(_butte);

        Assert.Empty(_errors);
        Assert.Equal(2, _combat.Spent.Count);
    }

    [Fact]
    public void AButte_WithArrowsStuckInIt_GivesThemBackToWhoStandsBesideIt()
    {
        _combat.Weapon = Bow;
        _butte.SetProp("butte.arrows", 3);
        var butte = _butte.GroundLocation!.Value;
        _aria.Location = new Point3D(butte.X + 1, butte.Y, butte.Z);

        Use(_butte);

        Assert.Empty(_errors);
        Assert.Equal([Gathered], Told());
        Assert.False(_butte.TryGetProp<int>("butte.arrows", out var left) && left > 0);
        Assert.Empty(_combat.Spent);
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }

    private void Use(ItemEntity item)
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(item, "on_use", Aria);

        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }

        _loop.DeferTryPost = false;
    }

    // What the butte says over itself to the player, as the shots' results.
    private List<int> Labels()
    {
        return _fixture.Sender.Sent.OfType<LocalizedMessagePacket>().Select(label => label.Cliloc).ToList();
    }

    private List<int> Told()
    {
        return _speech.ToldClilocs.Select(told => told.Cliloc).ToList();
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
