using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Crafts;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Harvest;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Items.Internal;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Effects;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Tests.Support.Timing;
using Moongate.Tests.TestSupport.Randomness;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Containers;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Tests.TestSupport.Ultima.Maps;
using Moongate.Server.Ultima.Interfaces.Items;
using Moongate.Tests.TestSupport.Ultima.Death;
using Moongate.Tests.TestSupport.Ultima.Effects;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.HuePicking;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Tests.TestSupport.Ultima.Combat;
using Moongate.Ultima.Types;
namespace Moongate.Tests.Integration.Server.Ultima.Items;

/// <summary>
///     The shipped <c>scripts/items/explosion_potion.lua</c>: armed, thrown, a countdown and an explosion on an area.
/// </summary>
public sealed class ExplosionPotionScriptIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;

    private const int TooFar = 500446;
    private const int ThrowItNow = 500236;
    private const int ExplosionSound = 0x307;
    private const int CannotSee = 500237;

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
    private readonly StubItemSerialPool _serials = new();
    private readonly StubContainerCapacityService _capacity = new();
    private readonly StubInventoryMutationGuard _guard = new();
    private readonly SettableClock _time = new();
    private readonly FakeMapService _map = new(32, 32, MapType.Trammel);
    private ItemService _items = null!;

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With<ItemTemplate>(
            new ItemTemplate { Id = "explosionpotion", ItemId = new Serial(0x0F0D), ScriptId = "explosion_potion", Stackable = true },
            new ItemTemplate { Id = "0x0f0d_b_purple_potion", ItemId = new Serial(0x0F0D), ScriptId = "explosion_potion", Stackable = true },
            new ItemTemplate { Id = "greaterexplosionpotion", ItemId = new Serial(0x0F0D), ScriptId = "explosion_potion", Stackable = true }
        )
    );

    private readonly RecordingCombatService _combat = new();

    private readonly CraftService _crafts = new(new StubDataLoaderService().With<CraftDefinition>());

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _bank = new()
        { Id = new Serial(0x40000003), TemplateId = "backpack", ItemId = 0x0E7C, Amount = 1 };

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private MobileEntity _aria = null!;
    private readonly HashSet<string> _fired = [];
    private uint _next = 0x40000050;


    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        // The sectors world.items_in_range reads: an anvil on the ground must be found there.
        _items = TestItems.Create(_fixture.Sectors);
        await _fixture.AddAsync((int)Aria);
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Aria), out _aria!));
        _aria.AccountId = new Serial(0x42);

        // At the most of the gloves, so it never fails unless a test lowers the skill; in a smithy at 10, 10.
        Skill(1000);
        _aria.Location = new Point3D(10, 10, 0);

        _backpack.Equip(new Serial((uint)Aria), LayerType.Backpack);
        _bank.Equip(new Serial((uint)Aria), LayerType.Bank);
        _items.Add([_backpack, _bank]);

        for (uint serial = 0x40000100; serial < 0x40000110; serial++)
        {
            _serials.Serials.Enqueue(new Serial(serial));
        }

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        _scripts.Write(
            "items/explosion_potion.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "explosion_potion.lua")) +
            """

            -- The damage of the test is the most of the potion.
            explosion_potion.random = function(low, high) return high end
            """
        );
        _scripts.Write("common/potions.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "potions.lua")));
        _scripts.Write("common/crafting.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "crafting.lua")));
        _scripts.Write("common/woods.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "woods.lua")));
        _scripts.Write("common/smithy.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "smithy.lua")));
        _scripts.Write("common/heat.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "heat.lua")));
        _scripts.Write("common/metals.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "metals.lua")));
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        var data = new StubDataLoaderService().With(
            new SkillContent { Id = SkillType.Cooking, GainFactor = 1.0, Delay = 1 }
        );

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
        _container.RegisterInstance<IContainerCapacityService>(_capacity);
        _container.RegisterInstance<IInventoryMutationGuard>(_guard);
        _container.RegisterInstance<TimeProvider>(_time);
        _container.RegisterInstance<IMapService>(_map);
        _container.Register<IItemHandlingService, ItemHandlingService>(Reuse.Singleton);
        _container.RegisterInstance<ICraftService>(_crafts);
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<ILineOfSightService>(_sight);
        _container.RegisterInstance<IMovementService>(_movement);
        _container.RegisterInstance<IDeathService>(new StubDeathService());
        _container.RegisterInstance<IEffectService>(_effects);
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<TargetModule>();
        _container.AddScriptModule<SkillModule>();
        _container.AddScriptModule<WorldModule>();
        _container.AddScriptModule<EffectModule>();
        _container.AddScriptModule<CraftModule>();
        _container.RegisterInstance<ICombatService>(_combat);
        _container.AddScriptModule<CombatModule>();
        _container.Register<IStatBonusService, StatBonusService>(Reuse.Singleton);
        _container.RegisterInstance(new CombatConfig());
        _container.Register<IPoisonService, PoisonService>(Reuse.Singleton, made: Parameters.Of.Type<Random>(_ => null));
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
    public void AnArmedPotion_NotThrown_ExplodesInTheHand_AndHurtsItsHolder()
    {
        var potion = Carry("explosionpotion", 0x0F0D, 1);

        Use(potion);

        Assert.Empty(_errors);
        Assert.Equal(ThrowItNow, Told()[0]);
        Countdown();

        Assert.Empty(_errors);
        Assert.Equal(0, Left(potion));
        Assert.Contains((_aria, _aria, 20), _combat.Harmed.Select(harm => (harm.Attacker, harm.Target, harm.Damage)));
        Assert.Contains(_speech.PlacedSounds, sound => sound.Sound == ExplosionSound && sound.Location == _aria.Location);
    }

    [Fact]
    public void AThrownPotion_LandsWhereItWasThrown_AndExplodesThere()
    {
        var orc = Npc(0x200, 14, 14);
        _targets.Result = TargetResult.ForLocation(_aria.Map, new Point3D(13, 14, 0));
        var potion = Carry("explosionpotion", 0x0F0D, 1);

        Use(potion);
        // Five tiles of flight (three across, four down), a tenth of a second each.
        Fire(0.5);

        Assert.Empty(_errors);
        Assert.Equal(0, Left(potion));
        var landed = Assert.Single(Made("explosionpotion").Where(item => item.GroundLocation is not null));
        Assert.Equal(new Point3D(13, 14, 0), landed.GroundLocation);

        Countdown();

        Assert.Empty(_errors);
        Assert.Contains((_aria, orc, 20), _combat.Harmed.Select(harm => (harm.Attacker, harm.Target, harm.Damage)));
        Assert.DoesNotContain(_combat.Harmed, harm => harm.Target == _aria);
        Assert.False(_items.TryGet(landed.Id, out _));
    }

    [Fact]
    public void AThrowTooFar_IsRefused_AndThePotionStaysArmedInTheHand()
    {
        _targets.Result = TargetResult.ForLocation(_aria.Map, new Point3D(25, 10, 0));
        var potion = Carry("explosionpotion", 0x0F0D, 1);

        Use(potion);

        Assert.Empty(_errors);
        Assert.Equal(TooFar, Told().Last());
        Assert.Equal(1, Left(potion));
        Assert.Equal(_aria.Id, _items.GetOwner(potion));
    }

    [Fact]
    public void FromAStack_OnlyOnePotionIsArmed()
    {
        var stack = Carry("explosionpotion", 0x0F0D, 3);

        Use(stack);

        Assert.Empty(_errors);
        Assert.Equal(2, Left(stack));
        var armed = Assert.Single(Made("explosionpotion"));
        Assert.Equal(1, armed.Amount);
        Assert.Equal(_aria.Id, _items.GetOwner(armed));
    }

    [Fact]
    public void APotionNearTheBlast_ExplodesToo()
    {
        var other = Ground("explosionpotion", 0x0F0D, 11, 10);
        var potion = Carry("explosionpotion", 0x0F0D, 1);

        Use(potion);
        Countdown();

        Assert.Empty(_errors);
        Assert.False(_items.TryGet(other.Id, out _));
        Assert.Equal(2, _speech.PlacedSounds.Count(sound => sound.Sound == ExplosionSound));
    }

    [Fact]
    public void TheDamage_IsAtMostForty()
    {
        AlchemySkill(1200);
        var potion = Carry("greaterexplosionpotion", 0x0F0D, 1);

        Use(potion);
        Countdown();

        Assert.Contains((_aria, _aria, 40), _combat.Harmed.Select(harm => (harm.Attacker, harm.Target, harm.Damage)));
    }

    [Fact]
    public void APotionHeldOnACursor_AtZero_GoesOffOnceItIsLetGo()
    {
        var potion = Carry("explosionpotion", 0x0F0D, 1);
        Use(potion);
        _guard.Allowed = false;

        Countdown();

        Assert.Empty(_combat.Harmed);
        Assert.Equal(1, Left(potion));

        _guard.Allowed = true;
        Fire(0.25);

        Assert.Empty(_errors);
        Assert.Equal(0, Left(potion));
        Assert.NotEmpty(_combat.Harmed);
    }

    [Fact]
    public void AStackHeldOnACursor_ArmsNothing()
    {
        var stack = Carry("explosionpotion", 0x0F0D, 3);
        _guard.Allowed = false;

        Use(stack);

        Assert.Empty(_errors);
        Assert.Equal(3, Left(stack));
        Assert.Empty(Made("explosionpotion"));
    }

    [Fact]
    public void AThrowerWhoLeft_StillHurtsThoseAround_WithNoOneToBlame()
    {
        var orc = Npc(0x200, 14, 14);
        _targets.Result = TargetResult.ForLocation(_aria.Map, new Point3D(13, 14, 0));
        Use(Carry("explosionpotion", 0x0F0D, 1));
        Fire(0.5);
        _fixture.Mobiles.LeaveWorld(_aria.Id);

        Countdown();

        Assert.Empty(_errors);
        Assert.Contains(((MobileEntity?)null, orc, 20), _combat.Harmed.Select(harm => (harm.Attacker, harm.Target, harm.Damage)));
    }

    [Fact]
    public void APotionOnTheGround_IsArmedInTheBackpack_AndCanBeThrown()
    {
        var potion = Ground("explosionpotion", 0x0F0D, 11, 10);
        _targets.Result = TargetResult.ForLocation(_aria.Map, new Point3D(13, 14, 0));

        Use(potion);
        Fire(0.5);

        Assert.Empty(_errors);
        Assert.False(_items.TryGet(potion.Id, out _));
        Assert.Single(Made("explosionpotion").Where(item => item.GroundLocation == new Point3D(13, 14, 0)));
    }

    [Fact]
    public void AThrowOutOfSight_IsRefused()
    {
        _sight.Allow = false;
        _targets.Result = TargetResult.ForLocation(_aria.Map, new Point3D(13, 14, 0));
        var potion = Carry("explosionpotion", 0x0F0D, 1);

        Use(potion);

        Assert.Equal(CannotSee, Told().Last());
        Assert.Equal(1, Left(potion));
    }

    [Fact]
    public void ACountdownThatEndsInFlight_GoesOffWhereThePotionLands()
    {
        var orc = Npc(0x200, 14, 14);
        var potion = Carry("explosionpotion", 0x0F0D, 1);
        Use(potion);
        Fire(0.75);
        Fire(1.0);
        Fire(1.0);
        // Thrown just before 0: the potion is still in the air at 0.
        _targets.Result = TargetResult.ForLocation(_aria.Map, new Point3D(13, 14, 0));
        Use(potion);
        Fire(1.0);

        Assert.Empty(_combat.Harmed);

        Fire(0.5);

        Assert.Empty(_errors);
        Assert.Contains(_combat.Harmed, harm => harm.Target == orc);
    }

    [Fact]
    public void APotionArmedBeforeARestart_IsArmedAfresh()
    {
        var potion = Carry("explosionpotion", 0x0F0D, 1);
        potion.SetProp("explosion.armed", 12345L);

        Use(potion);
        Countdown();

        Assert.Empty(_errors);
        Assert.Equal(0, Left(potion));
        Assert.NotEmpty(_combat.Harmed);
    }

    [Fact]
    public void AnExplosionPotionAsVendorsSellIt_GoesOffAsTheNamedOne()
    {
        var potion = Carry("0x0f0d_b_purple_potion", 0x0F0D, 1);

        Use(potion);
        Countdown();

        Assert.Empty(_errors);
        Assert.Contains((_aria, _aria, 20), _combat.Harmed.Select(harm => (harm.Attacker, harm.Target, harm.Damage)));
    }

    private void Use(ItemEntity potion)
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(potion, "on_use", Aria);

        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }

        _loop.DeferTryPost = false;
    }

    // 0.75 seconds to the first number, then one a second: 3, 2, 1, and the blast.
    private void Countdown()
    {
        Fire(0.75);
        Fire(1.0);
        Fire(1.0);
        Fire(1.0);
    }

    private MobileEntity Npc(uint serial, int x, int y)
    {
        var npc = new MobileEntity
        {
            Id = new Serial(serial), Name = "an orc", TemplateId = "orc", Map = _aria.Map, Location = new Point3D(x, y, 0),
            Hits = 50, HitsMax = 50
        };
        _fixture.Mobiles.EnterWorld(npc);

        return npc;
    }

    private void AlchemySkill(int tenths)
    {
        _aria.Skills.RemoveAll(known => known.Skill == SkillType.Alchemy);
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Alchemy, Base = tenths });
        _state.Skills.RemoveAll(known => known.Skill == SkillType.Alchemy);
        _state.Skills.Add(new MobileSkill { Skill = SkillType.Alchemy, Base = tenths });
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        await _fixture.DisposeAsync();
        _scripts.Dispose();
    }

    private void Skill(int tenths)
    {
        _aria.Skills.RemoveAll(known => known.Skill == SkillType.Cooking);
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Cooking, Base = tenths });
        _state.Skills.RemoveAll(known => known.Skill == SkillType.Cooking);
        _state.Skills.Add(new MobileSkill { Skill = SkillType.Cooking, Base = tenths });
    }

    // Fires the oldest timer of that many seconds that has not fired yet.
    private void Fire(double seconds)
    {
        var timer = _timers.Timers.First(timer => !_fired.Contains(timer.Id) &&
                                                  Math.Abs(timer.Interval.TotalSeconds - seconds) < 0.001
        );
        _fired.Add(timer.Id);
        _loop.DeferTryPost = true;
        _timers.Fire(timer.Id);

        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }

        _loop.DeferTryPost = false;
    }

    private ItemEntity Carry(string template, int graphic, int amount)
    {
        var item = new ItemEntity { Id = new Serial(_next++), TemplateId = template, ItemId = graphic, Amount = amount };
        item.PutInContainer(_backpack.Id, new Point2D(70, 70));
        _items.Add([item]);

        return item;
    }

    private ItemEntity Ground(string template, int graphic, int x, int y)
    {
        var item = new ItemEntity { Id = new Serial(_next++), TemplateId = template, ItemId = graphic, Amount = 1 };
        _items.Add([item]);
        _items.PlaceOnGround(item, _aria.Map, new Point3D(x, y, 0));

        return item;
    }

    private int Left(ItemEntity stack)
    {
        return _items.TryGet(stack.Id, out var still) ? still.Amount : 0;
    }

    // The items of a template made from the serials the pool gives.
    private List<ItemEntity> Made(string template)
    {
        var made = new List<ItemEntity>();

        for (uint serial = 0x40000100; serial < 0x40000110; serial++)
        {
            if (_items.TryGet(new Serial(serial), out var item) && item.TemplateId == template)
            {
                made.Add(item);
            }
        }

        return made;
    }

    private List<int> Told()
    {
        return _speech.ToldClilocs.Where(told => told.Player == _aria).Select(told => told.Cliloc).ToList();
    }

    private List<string> Opened()
    {
        return _speech.Told.Where(told => told.Player == _aria && told.Text.StartsWith("opened", StringComparison.Ordinal))
            .Select(told => told.Text.TrimEnd())
            .ToList();
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
