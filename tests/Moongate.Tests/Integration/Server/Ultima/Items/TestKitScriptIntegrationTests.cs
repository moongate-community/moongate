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
using Moongate.Ultima.Types;
namespace Moongate.Tests.Integration.Server.Ultima.Items;

/// <summary>
///     The shipped <c>scripts/items/test_kit.lua</c>: a staff test bag fills with what it is for, once, when it is first opened.
/// </summary>
public sealed class TestKitScriptIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;

    private const int TooFar = 502138;
    private const int NoFreeHand = 502172;
    private const int FullHealth = 1049547;
    private const int HealWait = 500235;
    private const int SimilarEffect = 502173;
    private const int CannotHeal = 1005000;
    private const int NotPoisoned = 1042000;
    private const int NotStrongEnough = 500232;
    private const int Cured = 500231;

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
            new ItemTemplate { Id = "test_kit_explosion", ItemId = new Serial(0x0E76), ScriptId = "test_kit" },
            new ItemTemplate { Id = "lesserexplosionpotion", ItemId = new Serial(0x0F0D), Stackable = true },
            new ItemTemplate { Id = "explosionpotion", ItemId = new Serial(0x0F0D), Stackable = true },
            new ItemTemplate { Id = "greaterexplosionpotion", ItemId = new Serial(0x0F0D), Stackable = true }
        )
    );

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
        _scripts.Write("items/test_kit.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "test_kit.lua")));
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
        // A bag is a container in the tiledata: what is moved into it goes inside.
        _container.RegisterInstance<ITileDataService>(new FakeTileDataService().Item(0x0E76, TileFlagType.Container, 0));
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
    public void ATestBag_FillsWhenFirstOpened_AndOnlyThen()
    {
        var bag = Carry("test_kit_explosion", 0x0E76, 1);

        Open(bag);
        Open(bag);

        Assert.Empty(_errors);
        var potions = Made("greaterexplosionpotion");
        Assert.Equal(5, Assert.Single(potions).Amount);
        Assert.Equal(bag.Id, potions[0].ContainerId);
        Assert.Equal(3, _items.GetContents(bag.Id).Count);
    }

    private void Open(ItemEntity bag)
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(bag, "on_use", Aria);

        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }

        _loop.DeferTryPost = false;
    }

    private void Drink(ItemEntity potion)
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(potion, "on_use", Aria);

        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }

        _loop.DeferTryPost = false;
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
