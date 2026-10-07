using DryIoc;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Data.Sessions;
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
using Moongate.Server.Core.Types.Accounts;
namespace Moongate.Tests.Integration.Server.Ultima.Items;

/// <summary>
///     The shipped <c>scripts/items/lockpick.lua</c> and <c>treasure_chest.lua</c>, with the real Lua engine.
/// </summary>
public sealed class LockpickScriptsIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;

    private const int What = 502068;
    private const int NotLocked = 502069;
    private const int CannotUnlock = 501666;
    private const int NormalMeans = 502073;
    private const int CannotManipulate = 502072;
    private const int Yields = 502076;
    private const int Unable = 502075;
    private const int Broke = 502074;
    private const int Locked = 501747;
    private const int Godly = 502502;

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
    private readonly SettableClock _clock = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly ItemService _items;

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "lockpick", ItemId = new Serial(0x14FC), ScriptId = "lockpick", Stackable = true },
            new ItemTemplate { Id = "treasure_chest_level_2", ItemId = new Serial(0x0E41), ScriptId = "treasure_chest" }
        )
    );

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _pick = new()
        { Id = new Serial(0x40000002), TemplateId = "lockpick", ItemId = 0x14FC, Amount = 5 };

    private readonly ItemEntity _chest = new()
        { Id = new Serial(0x40000003), TemplateId = "treasure_chest_level_2", ItemId = 0x0E41, Amount = 1 };

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private MobileEntity _aria = null!;
    private GameSession _session = null!;

    public LockpickScriptsIntegrationTests()
    {
        _items = TestItems.Create(_sectors);
    }

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync((int)Aria);
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Aria), out _aria!));
        _aria.AccountId = new Serial(0x42);
        _backpack.Equip(_aria.Id, LayerType.Backpack);
        _pick.PutInContainer(_backpack.Id, new Point2D(44, 65));
        _chest.PlaceOnGround(_aria.Map, new Point3D(_aria.Location.X + 1, _aria.Location.Y, _aria.Location.Z));
        _items.Add([_backpack, _pick, _chest]);

        var data = new StubDataLoaderService().With(
            new SkillContent { Id = SkillType.Lockpicking, GainFactor = 1.0, Delay = 1 }
        );
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        var root = Path.Combine(RepositoryRoot(), "moongate_root", "scripts", "items");

        foreach (var script in new[] { "lockpick.lua", "treasure_chest.lua" })
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
        _container.RegisterInstance<ITargetService>(_targets);
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
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<TargetModule>();
        _container.AddScriptModule<SkillModule>();
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
    public void ANewChest_IsLockedWithTheLockOfItsLevel()
    {
        Create();

        Assert.Empty(_errors);
        Assert.True(_chest.GetProp<bool>("locked"));
        Assert.Equal(72, _chest.GetProp<long>("lock.required"));
        Assert.InRange(_chest.GetProp<long>("lock.level"), 62, 71);
        Assert.InRange(_chest.GetProp<long>("lock.max"), 73, 82);
    }

    [Fact]
    public async Task ALockedChest_DoesNotOpen_ForAPlayer_ButOpensForAGameMaster()
    {
        Create();

        var player = _itemScripts.Run(_chest, "on_use", Aria);
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.GameMaster));
        var staff = _itemScripts.Run(_chest, "on_use", Aria);

        Assert.Empty(_errors);
        Assert.Equal((ScriptResultKind.Completed, true), (player.Kind, player.Values[0]));
        Assert.Equal(ScriptResultKind.Completed, staff.Kind);
        Assert.Empty(staff.Values);
        Assert.Equal([Locked, Godly], Labels());
    }

    [Fact]
    public void AChestThatIsNotLocked_OpensAsAnyContainer()
    {
        var result = _itemScripts.Run(_chest, "on_use", Aria);

        Assert.Empty(_errors);
        Assert.Empty(result.Values);
        Assert.Empty(Labels());
    }

    [Fact]
    public void ALockpick_WithEnoughSkill_PicksTheLock_ForGood_AfterThreeSeconds()
    {
        Lock(required: 50, level: 40, max: 60);
        Skills(1000);
        _targets.Result = TargetResult.ForObject(_chest.Id);

        Use();

        Assert.Equal([What], Told());
        Assert.Equal(TimeSpan.FromSeconds(3), Assert.Single(_timers.Timers).Interval);
        Assert.True(_chest.GetProp<bool>("locked"));

        _timers.Fire(_timers.Timers[0].Id);

        Assert.Empty(_errors);
        Assert.False(_chest.GetProp<bool>("locked"));
        Assert.Equal((long)_aria.Id.Value, _chest.GetProp<long>("lock.picker"));
        Assert.Equal([Yields], Labels());
        Assert.Equal(5, _pick.Amount);

        // It opens now.
        Assert.Empty(_itemScripts.Run(_chest, "on_use", Aria).Values);
    }

    [Fact]
    public void ALockpick_WithTooLittleSkillForTheTry_SaysItSeesNoWay_AndOneThatCannotBePicked_SaysSo()
    {
        Lock(required: 50, level: 40, max: 60);
        Skills(300);
        _targets.Result = TargetResult.ForObject(_chest.Id);
        Use();
        _timers.Fire(_timers.Timers[0].Id);

        _chest.SetProp("lock.level", null);
        Use();
        _timers.Fire(_timers.Timers[0].Id);

        Assert.Empty(_errors);
        Assert.Equal([CannotManipulate, NormalMeans], Labels());
        Assert.True(_chest.GetProp<bool>("locked"));
    }

    [Fact]
    public void ALockpick_ThatFails_SaysSo_AndMayBreak()
    {
        Lock(required: 10, level: 40, max: 60);
        Skills(450);
        _random.Doubles(0.9);
        _targets.Result = TargetResult.ForObject(_chest.Id);

        Use();
        _timers.Fire(_timers.Timers[0].Id);

        Assert.Empty(_errors);
        Assert.True(_chest.GetProp<bool>("locked"));
        Assert.Contains(Unable, Labels());
        Assert.Equal(Labels().Contains(Broke) ? 4 : 5, _pick.Amount);
    }

    [Fact]
    public void ALockpick_OnWhatIsNotLocked_OrIsNotAnItemInReach_SaysSo()
    {
        _targets.Result = TargetResult.ForObject(_chest.Id);
        Use();

        _targets.Result = TargetResult.ForObject(new Serial(0x40000999));
        Use();

        Assert.Empty(_errors);
        Assert.Equal([What, What], Told());
        Assert.Equal([NotLocked], Labels());
        Assert.Equal([CannotUnlock], _speech.ToldClilocs.Select(told => told.Cliloc).Where(cliloc => cliloc == CannotUnlock));
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void ALockpick_WhenThePlayerWalksAwayDuringThePicking_PicksNothing()
    {
        Lock(required: 50, level: 40, max: 60);
        Skills(1000);
        _targets.Result = TargetResult.ForObject(_chest.Id);
        Use();

        _aria.Location = new Point3D(_aria.Location.X - 5, _aria.Location.Y, _aria.Location.Z);
        _timers.Fire(_timers.Timers[0].Id);

        Assert.Empty(_errors);
        Assert.True(_chest.GetProp<bool>("locked"));
        Assert.Empty(Labels());
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }

    private void Create()
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(_chest, "on_create", (long)_chest.Id.Value);
        _loop.DeferTryPost = false;
    }

    private void Lock(int required, int level, int max)
    {
        _chest.SetProp("locked", true);
        _chest.SetProp("lock.required", (long)required);
        _chest.SetProp("lock.level", (long)level);
        _chest.SetProp("lock.max", (long)max);
    }

    private void Skills(int tenths)
    {
        _state.Skills.Clear();
        _state.Skills.Add(new MobileSkill { Skill = SkillType.Lockpicking, Base = tenths });
        _aria.Skills = [new MobileSkill { Skill = SkillType.Lockpicking, Base = tenths }];
    }

    private void Use()
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(_pick, "on_use", Aria);

        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }

        _loop.DeferTryPost = false;
    }

    private List<int> Told()
    {
        return _speech.ToldClilocs.Select(told => told.Cliloc).Where(cliloc => cliloc == What).ToList();
    }

    // What the lock says over itself to the player.
    private List<int> Labels()
    {
        return _fixture.Sender.Sent.OfType<LocalizedMessagePacket>().Select(label => label.Cliloc).ToList();
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
