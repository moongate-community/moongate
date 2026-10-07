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

namespace Moongate.Tests.Integration.Server.Ultima.Items;

/// <summary>
///     The shipped <c>scripts/items/bandage.lua</c>, with the real Lua engine: Healing as ModernUO's classic one.
/// </summary>
public sealed class BandageScriptIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;
    private const long Bruno = 3;

    private const int Who = 500948;
    private const int Begin = 500956;
    private const int NotDamaged = 500955;
    private const int TooFar = 500295;
    private const int Finished = 500969;
    private const int NotClose = 500963;
    private const int Raised = 500965;
    private const int Attempting = 1008078;
    private const int CannotSee = 500237;
    private const int GhostBody = 0x0192;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly RecordingGumpService _gumps = new();
    private readonly StubTargetService _targets = new();
    private readonly ScriptedRandom _random = new();
    private readonly StubDeathService _death = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly RecordingEffectService _effects = new();
    private readonly ItemService _items;

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "bandage", ItemId = new Serial(0x0E21), ScriptId = "bandage", Stackable = true }
        )
    );

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _bandage = new()
        { Id = new Serial(0x40000002), TemplateId = "bandage", ItemId = 0x0E21, Amount = 5 };

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _bruno = null!;
    private GameSession _brunoSession = null!;

    public BandageScriptIntegrationTests()
    {
        _items = TestItems.Create(_sectors);
    }

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync((int)Aria);
        _brunoSession = await _fixture.AddAsync((int)Bruno);
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Aria), out _aria!));
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Bruno), out _bruno!));

        // Two players: a healer that heals whatever it tries, and a hurt friend beside it.
        _aria.AccountId = new Serial(0x42);
        _bruno.AccountId = new Serial(0x43);
        _aria.Dexterity = 100;
        Skills(1200, 1200);
        _bruno.HitsMax = 100;
        _bruno.Hits = 10;
        _bruno.Location = new Point3D(_aria.Location.X + 1, _aria.Location.Y, _aria.Location.Z);

        _backpack.Equip(new Serial((uint)Aria), LayerType.Backpack);
        _bandage.PutInContainer(_backpack.Id, new Point2D(44, 65));
        _items.Add([_backpack, _bandage]);

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        _scripts.Write("items/bandage.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "bandage.lua")));
        _scripts.Write(
            "gumps/resurrect.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "gumps", "resurrect.lua"))
        );
        var gumpTemplates =
            (await new GumpsLoader(new DirectoriesConfig(root, ["templates"])).LoadDataAsync()).Entities.ToArray();
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        var data = new StubDataLoaderService().With(
            new SkillContent { Id = SkillType.Healing, GainFactor = 1.0, Delay = 1 },
            new SkillContent { Id = SkillType.Anatomy, GainFactor = 1.0, Delay = 1 }
        );

        GumpScriptService? gumpScripts = null;
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
        _container.RegisterInstance<IGumpService>(_gumps);
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With(gumpTemplates), _loop, _fixture.Sessions)
        );
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
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
        _container.AddScriptModule<GumpModule>();
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<ILineOfSightService>(_sight);
        _container.AddScriptModule<WorldModule>();
        _container.RegisterInstance<IDeathService>(_death);
        _container.RegisterInstance<IEffectService>(_effects);
        _container.RegisterScriptEnum<EffectGraphicType>();
        _container.AddScriptModule<EffectModule>();
        _container.RegisterScriptEnum<BodyType>();
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
        gumpScripts = new GumpScriptService(_engine, _loop, options);
        await gumpScripts.StartAsync();
        _itemScripts = new(_engine, _templates, _loop, options);
        await _itemScripts.StartAsync();
    }

    [Fact]
    public void Bandage_OnAHurtFriend_TakesABandage_AndHealsWhenTheWaitIsOver()
    {
        _targets.Result = TargetResult.ForObject(_bruno.Id);

        Use();

        Assert.Empty(_errors);
        Assert.Equal([Who, Begin], Told(_aria));
        Assert.Equal([Attempting], Told(_bruno));
        Assert.Equal(4, _bandage.Amount);
        Assert.Equal(10, _bruno.Hits);

        // 100 dexterity: three seconds for someone else.
        var timer = Assert.Single(_timers.Timers);
        Assert.Equal(TimeSpan.FromSeconds(3), timer.Interval);
        _timers.Fire(timer.Id);

        Assert.Empty(_errors);
        Assert.Equal(Finished, Told(_aria)[^1]);

        // Anatomy and Healing at 120: from 20+24+3 = 47 to 20+60+10 = 90 hit points, never above the maximum.
        Assert.InRange(_bruno.Hits, 10 + 47, 100);
    }

    [Fact]
    public void Bandage_OnItself_WaitsLonger()
    {
        _aria.HitsMax = 100;
        _aria.Hits = 50;
        _targets.Result = TargetResult.ForObject(_aria.Id);

        Use();

        Assert.Empty(_errors);

        // 9.4 + 0.6 * (120 - 100) / 10 = 10.6 seconds.
        Assert.Equal(10.6, Assert.Single(_timers.Timers).Interval.TotalSeconds, 3);
        Assert.Equal([Who, Begin], Told(_aria));
    }

    [Fact]
    public void Bandage_OnSomeoneNotHurt_SaysSo_AndKeepsTheBandage()
    {
        _bruno.Hits = 100;
        _targets.Result = TargetResult.ForObject(_bruno.Id);

        Use();

        Assert.Empty(_errors);
        Assert.Equal([Who, NotDamaged], Told(_aria));
        Assert.Equal(5, _bandage.Amount);
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Bandage_OnSomeoneOutOfReach_SaysItIsTooFar()
    {
        _bruno.Location = new Point3D(_aria.Location.X + 3, _aria.Location.Y, _aria.Location.Z);
        _targets.Result = TargetResult.ForObject(_bruno.Id);

        Use();

        Assert.Empty(_errors);
        Assert.Equal([Who, TooFar], Told(_aria));
        Assert.Equal(5, _bandage.Amount);
    }

    [Fact]
    public void Bandage_TheHealerWalksAway_TheHealingIsLost()
    {
        _targets.Result = TargetResult.ForObject(_bruno.Id);
        Use();

        _bruno.Location = new Point3D(_aria.Location.X + 4, _aria.Location.Y, _aria.Location.Z);
        _timers.Fire(Assert.Single(_timers.Timers).Id);

        Assert.Empty(_errors);
        Assert.Equal(NotClose, Told(_aria)[^1]);
        Assert.Equal(10, _bruno.Hits);
        Assert.Equal(4, _bandage.Amount);
    }

    [Fact]
    public void Bandage_ASecondOne_ReplacesTheFirst()
    {
        _targets.Result = TargetResult.ForObject(_bruno.Id);
        Use();
        var first = Assert.Single(_timers.Timers);
        Use();

        var timers = _timers.Timers.ToList();
        Assert.Equal(2, timers.Count);

        // The old one is told no more: its wait ends and nothing happens.
        _timers.Fire(first.Id);
        Assert.Equal(10, _bruno.Hits);
        _timers.Fire(timers[1].Id);

        Assert.Empty(_errors);
        Assert.True(_bruno.Hits > 10);
        Assert.Equal(3, _bandage.Amount);
    }

    [Fact]
    public void Bandage_OnAGhost_AsksItToComeBack_WithFiveSecondsMore()
    {
        _bruno.Body = GhostBody;
        _targets.Result = TargetResult.ForObject(_bruno.Id);

        Use();

        // 3 seconds and 5 more to raise it.
        var timer = Assert.Single(_timers.Timers);
        Assert.Equal(TimeSpan.FromSeconds(8), timer.Interval);
        _timers.Fire(timer.Id);

        Assert.Empty(_errors);
        Assert.Equal(Raised, Told(_aria)[^1]);
        Assert.Equal("resurrect", Assert.Single(_gumps.Opened).Gump.Id);
    }

    [Fact]
    public void Bandage_TheGhostThatAgrees_ComesBack_AndLosesATenthOfItsFame_AsAtAnAnkh()
    {
        _bruno.Body = GhostBody;
        _bruno.Fame = 1000;
        _targets.Result = TargetResult.ForObject(_bruno.Id);
        Use();
        _timers.Fire(Assert.Single(_timers.Timers).Id);

        // The Continue button, away from the healer: a bandage asks the ghost once and keeps no reach.
        _bruno.Location = new Point3D(_aria.Location.X + 12, _aria.Location.Y, _aria.Location.Z);
        _gumps.Opened[0]
            .Gump.OnResponse(
                _brunoSession,
                new GumpResponse { ButtonId = 1, Switches = new HashSet<int>(), Texts = new Dictionary<int, string>() }
            );

        Assert.Empty(_errors);
        Assert.Equal([_bruno], _death.PlayersRaised);
        Assert.Equal(900, _bruno.Fame);
    }

    [Fact]
    public void Bandage_AFailedRaise_TeachesNothing()
    {
        Skills(790, 1200);
        _bruno.Body = GhostBody;
        _targets.Result = TargetResult.ForObject(_bruno.Id);

        Use();
        _timers.Fire(Assert.Single(_timers.Timers).Id);

        Assert.Empty(_errors);
        Assert.Equal(0, _random.Rolls);
    }

    [Fact]
    public void Bandage_OnSomeoneHidden_OrBehindAWall_CannotBeSeen_AndKeepsTheBandage()
    {
        _bruno.Hidden = true;
        _targets.Result = TargetResult.ForObject(_bruno.Id);
        Use();

        _bruno.Hidden = false;
        _sight.Allow = false;
        Use();

        Assert.Empty(_errors);
        Assert.Equal([Who, CannotSee, Who, CannotSee], Told(_aria));
        Assert.Equal(5, _bandage.Amount);
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Bandage_RevealsAHiddenHealer()
    {
        _aria.Hidden = true;
        _targets.Result = TargetResult.Canceled(TargetCancelType.Canceled);

        Use();

        Assert.False(_aria.Hidden);
    }

    [Fact]
    public void Bandage_AHealerThatDiedWhileTheCursorWasOpen_UsesNoBandage()
    {
        _aria.Body = GhostBody;
        _targets.Result = TargetResult.ForObject(_bruno.Id);

        Use();

        Assert.Empty(_errors);
        Assert.Equal(5, _bandage.Amount);
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Bandage_OnAGhost_WithTooLittleSkill_IsUnableToRaiseIt()
    {
        Skills(790, 1200);
        _bruno.Body = GhostBody;
        _targets.Result = TargetResult.ForObject(_bruno.Id);

        Use();
        _timers.Fire(Assert.Single(_timers.Timers).Id);

        Assert.Empty(_errors);
        Assert.Empty(_gumps.Opened);
        Assert.Equal(500966, Told(_aria)[^1]);
    }

    [Fact]
    public void Bandage_WhenTheCursorIsPutAway_DoesNothingMore()
    {
        _targets.Result = TargetResult.Canceled(TargetCancelType.Canceled);

        Use();

        Assert.Empty(_errors);
        Assert.Equal([Who], Told(_aria));
        Assert.Equal(5, _bandage.Amount);
        Assert.Empty(_timers.Timers);
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        await _fixture.DisposeAsync();
        _scripts.Dispose();
    }

    private void Skills(int healing, int anatomy)
    {
        _state.Skills.Clear();
        _state.Skills.Add(new MobileSkill { Skill = SkillType.Healing, Base = healing });
        _state.Skills.Add(new MobileSkill { Skill = SkillType.Anatomy, Base = anatomy });
    }

    private void Use()
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(_bandage, "on_use", Aria);

        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }

        _loop.DeferTryPost = false;
    }

    private List<int> Told(MobileEntity mobile)
    {
        return _speech.ToldClilocs.Where(told => told.Player == mobile).Select(told => told.Cliloc).ToList();
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
