using DryIoc;
using Moongate.Server.Ultima.Types.Combat;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Randomness;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Combat;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Tests.TestSupport.Ultima.Combat;
namespace Moongate.Tests.Integration.Server.Ultima.Skills;

/// <summary>
///     The shipped <c>scripts/skills/stealth.lua</c> and <c>snooping.lua</c>, with the real Lua engine, skill check and
///     container view.
/// </summary>
public sealed class StealthAndSnoopingScriptIntegrationTests : IAsyncLifetime
{
    private const long Bruno = 3;

    private const int HideFirst = 502725;
    private const int NotHiddenWell = 502726;
    private const int TooMuchArmor = 502727;
    private const int Begin = 502730;
    private const int Failed = 502731;
    private const int TooFar = 500446;
    private const int NegativeActs = 1001018;
    private const int FailedToPeek = 500210;
    private const int BackpackGraphic = 0x0E75;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly StubCombatGearService _gear = new();
    private readonly RecordingCombatService _combat = new();
    private readonly ScriptedRandom _random = new();
    private readonly SettableClock _time = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly ItemService _items;
    private readonly FakeTileDataService _tiles = new FakeTileDataService().Item(BackpackGraphic, TileFlagType.Container, 0);

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000020), TemplateId = "backpack", ItemId = BackpackGraphic, Amount = 1 };

    private readonly ItemEntity _coin = new()
        { Id = new Serial(0x40000021), TemplateId = "gold", ItemId = 0x0EED, Amount = 5 };

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private SkillScriptService _skillScripts = null!;
    private SkillUseService _use = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _bruno = null!;

    public StealthAndSnoopingScriptIntegrationTests()
    {
        _items = TestItems.Create(_sectors);
    }

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        await _fixture.AddAsync((int)Bruno);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Bruno), out _bruno!));
        _aria.AccountId = new Serial(0x42);
        _bruno.AccountId = new Serial(0x43);
        _bruno.Location = new Point3D(_aria.Location.X + 1, _aria.Location.Y, _aria.Location.Z);
        _bruno.Map = _aria.Map;
        _backpack.Equip(_bruno.Id, LayerType.Backpack);
        _coin.PutInContainer(_backpack.Id, new Point2D(44, 65));
        _items.Add([_backpack, _coin]);

        var data = new StubDataLoaderService().With(
            new SkillContent { Id = SkillType.Stealth, GainFactor = 1.0, Delay = 10 },
            new SkillContent { Id = SkillType.Snooping, GainFactor = 1.0, Delay = 1 }
        );
        var layouts = new ContainerLayoutService(
            new StubDataLoaderService().With(
                new ContainerContent { Name = "backpack", Gump = 0x003C, Items = [BackpackGraphic], Default = true }
            )
        );
        var tooltips = TestTooltips.Create(_items, _fixture.Mobiles);
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IItemService>(_items);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<IMobileStateService>(_state);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<ITooltipService>(tooltips);
        _container.RegisterInstance<ITileDataService>(_tiles);
        _container.RegisterInstance<IContainerLayoutService>(layouts);
        _container.RegisterInstance<IContainerViewService>(new ContainerViewService(_items, layouts, _fixture.Sender, tooltips));
        _container.RegisterInstance<ICombatGearService>(_gear);
        _container.RegisterInstance<ICombatService>(_combat);
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
        _container.RegisterInstance<ISkillService>(new SkillService(_state, data, new SkillsConfig(), _random));
        _container.Register<IItemHandlingService, ItemHandlingService>(Reuse.Singleton);
        _container.RegisterScriptEnum<BodyType>();
        _container.RegisterScriptEnum<MapType>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<SkillModule>();
        _container.AddScriptModule<CombatModule>();
        _container.AddScriptModule<WorldModule>();
        _container.RegisterDelegate<IScriptEngine>(_ => _engine);
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );

        foreach (var script in new[] { "stealth", "snooping" })
        {
            _scripts.Write($"skills/{script}.lua", File.ReadAllText(ShippedScript($"skills/{script}.lua")));
        }

        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path,
            MaxInstructionsPerResume = 20_000,
            MaxInstructionsPerChunk = 100_000,
            HookInterval = 100
        };
        _engine = new(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
        await _engine.StartAsync();
        _skillScripts = new SkillScriptService(_engine, _loop, options);
        await _skillScripts.StartAsync();
        _use = new(_fixture.Mobiles, _skillScripts, _speech, _time, data);
    }

    [Fact]
    public void Stealth_NotHidden_AsksToHideFirst()
    {
        Skills((SkillType.Hiding, 1000), (SkillType.Stealth, 1000));

        _use.Use(_session, SkillType.Stealth);

        Assert.Empty(_errors);
        Assert.Equal([HideFirst], Told(_aria));
    }

    [Fact]
    public void Stealth_WithTooLittleHiding_OrTooMuchArmor_SaysSo_AndShowsThePlayer()
    {
        Skills((SkillType.Hiding, 790), (SkillType.Stealth, 1000));
        _aria.Hidden = true;
        _use.Use(_session, SkillType.Stealth);
        Assert.False(_aria.Hidden);

        _time.Advance(TimeSpan.FromSeconds(11));
        Skills((SkillType.Hiding, 1000), (SkillType.Stealth, 1000));
        _gear.Armor[ArmorZoneType.Chest] = 26;
        _aria.Hidden = true;
        _use.Use(_session, SkillType.Stealth);

        Assert.Empty(_errors);
        Assert.False(_aria.Hidden);
        Assert.Equal([NotHiddenWell, TooMuchArmor], Told(_aria));
    }

    [Fact]
    public void Stealth_ThatPasses_AllowsAStepForEveryTenPoints_AndTheTryThatFailsShowsThePlayer()
    {
        Skills((SkillType.Hiding, 1000), (SkillType.Stealth, 1000));
        _aria.Hidden = true;

        _use.Use(_session, SkillType.Stealth);

        // 100 points: ten steps. At the most of the skill check the try cannot fail.
        Assert.Empty(_errors);
        Assert.True(_aria.Hidden);
        Assert.Equal(10, _aria.AllowedStealthSteps);
        Assert.Equal([Begin], Told(_aria));

        // With no skill at all the try is under the least that can pass and fails.
        _time.Advance(TimeSpan.FromSeconds(11));
        Skills((SkillType.Hiding, 1000), (SkillType.Stealth, 0));
        _aria.AllowedStealthSteps = 0;
        _use.Use(_session, SkillType.Stealth);

        Assert.False(_aria.Hidden);
        Assert.Equal([Begin, Failed], Told(_aria));
    }

    [Fact]
    public void Stealth_WithLittleSkill_AllowsAtLeastOneStep()
    {
        // 20 points: between -20 and 80 the chance is 0.4; the roll 0.1 passes. Two steps from a tenth of 20.
        Skills((SkillType.Hiding, 1000), (SkillType.Stealth, 200));
        _random.Doubles(0.1);
        _aria.Hidden = true;

        _use.Use(_session, SkillType.Stealth);

        Assert.Empty(_errors);
        Assert.Equal(2, _aria.AllowedStealthSteps);
    }

    [Fact]
    public void Snooping_WithEnoughSkill_ShowsTheBackpackToThePlayer_AndCostsFourKarma()
    {
        Skills((SkillType.Snooping, 1000));
        _aria.Karma = 100;

        Snoop();

        Assert.Empty(_errors);
        Assert.Equal(96, _aria.Karma);
        Assert.Contains(_fixture.Sender.Sent, packet => packet is DisplayContainerPacket);
        Assert.Contains(_fixture.Sender.Sent, packet => packet is ContainerContentPacket);
    }

    [Fact]
    public void Snooping_WithNoSkill_FailsToPeek_AndShowsNothing()
    {
        Skills((SkillType.Snooping, 0));
        _random.Doubles(0.9);

        Snoop();

        Assert.Empty(_errors);
        Assert.Equal([FailedToPeek], Told(_aria));
        Assert.DoesNotContain(_fixture.Sender.Sent, packet => packet is DisplayContainerPacket);
    }

    [Fact]
    public void Snooping_FromAFarPlayer_SaysItIsTooFar_AGameMaster_NeedsNotBeNear()
    {
        Skills((SkillType.Snooping, 1000));
        _bruno.Location = new Point3D(_aria.Location.X + 5, _aria.Location.Y, _aria.Location.Z);
        Snoop();

        Assert.Equal([TooFar], Told(_aria));
        Assert.DoesNotContain(_fixture.Sender.Sent, packet => packet is DisplayContainerPacket);
    }

    [Fact]
    public async Task Snooping_AGameMasterOwner_CannotBeSnooped_AndTheStaffSeesWithoutTheSkill()
    {
        Skills((SkillType.Snooping, 0));
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Sessions.GetAll().Single(session => session.CharacterId == _bruno.Id).Set(SessionKeys.AccountType, AccountType.GameMaster));
        Snoop();
        Assert.Equal([NegativeActs], Told(_aria));

        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Sessions.GetAll().Single(session => session.CharacterId == _bruno.Id).Set(SessionKeys.AccountType, AccountType.Regular));
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.GameMaster));
        Snoop();

        Assert.Empty(_errors);
        Assert.Contains(_fixture.Sender.Sent, packet => packet is DisplayContainerPacket);
    }

    [Fact]
    public void Snooping_ADeadOwner_DoesNothing_AndAnInvulnerablePlayer_CannotBePerformedOn()
    {
        Skills((SkillType.Snooping, 1000));
        _bruno.Body = 0x0192;
        Snoop();
        Assert.Empty(Told(_aria));

        _bruno.Body = 400;
        _bruno.Notoriety = NotorietyType.Invulnerable;
        Snoop();

        Assert.Empty(_errors);
        Assert.Equal([NegativeActs], Told(_aria));
        Assert.DoesNotContain(_fixture.Sender.Sent, packet => packet is DisplayContainerPacket);
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }

    private void Skills(params (SkillType Skill, int Tenths)[] skills)
    {
        _state.Skills.Clear();
        _state.Skills.AddRange(skills.Select(skill => new MobileSkill { Skill = skill.Skill, Base = skill.Tenths }));
        _aria.Skills = skills.Select(skill => new MobileSkill { Skill = skill.Skill, Base = skill.Tenths }).ToList();
    }

    private void Snoop()
    {
        _loop.DeferTryPost = true;
        _skillScripts.Call(SkillType.Snooping, "on_snoop", (long)_aria.Id.Value, (long)_bruno.Id.Value, (long)_backpack.Id.Value);

        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }

        _loop.DeferTryPost = false;
    }

    private List<int> Told(MobileEntity who)
    {
        return _speech.ToldClilocs.Where(told => told.Player == who).Select(told => told.Cliloc).ToList();
    }

    private static string ShippedScript(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "moongate_root", "scripts", relativePath);
    }
}
