using DryIoc;
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
namespace Moongate.Tests.Integration.Server.Ultima.Skills;

/// <summary>
///     The shipped <c>scripts/skills/</c> of Anatomy, Evaluating Intelligence, Forensic Evaluation and Detecting Hidden,
///     with the real Lua engine, skill check and skill use.
/// </summary>
public sealed class LoreSkillsScriptIntegrationTests : IAsyncLifetime
{
    private const long Bruno = 3;

    private const int Whom = 500321;
    private const int Yourself = 500324;
    private const int NotAlive = 500323;
    private const int CanNotSense = 1042666;
    private const int What = 500906;
    private const int Silly = 500910;
    private const int ShowMe = 501000;
    private const int NothingUseful = 501001;
    private const int Where = 500819;
    private const int Revealed = 500814;
    private const int NothingHidden = 500817;
    private const int KilledBy = 1042751;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly StubTargetService _targets = new();
    private readonly ScriptedRandom _random = new();
    private readonly SettableClock _time = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly ItemService _items;

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private SkillUseService _use = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _bruno = null!;

    public LoreSkillsScriptIntegrationTests()
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
        _bruno.Location = new Point3D(_aria.Location.X + 2, _aria.Location.Y, _aria.Location.Z);
        _bruno.Strength = 100;
        _bruno.Dexterity = 50;
        _bruno.Intelligence = 100;
        _bruno.HitsMax = 100;
        _bruno.Hits = 100;
        _bruno.StaminaMax = 100;
        _bruno.Stamina = 50;
        _bruno.ManaMax = 100;
        _bruno.Mana = 50;

        var data = new StubDataLoaderService().With(
            new SkillContent { Id = SkillType.Anatomy, GainFactor = 1.0, Delay = 1 },
            new SkillContent { Id = SkillType.EvaluatingIntelligence, GainFactor = 1.0, Delay = 1 },
            new SkillContent { Id = SkillType.ForensicEvaluation, GainFactor = 1.0, Delay = 1 },
            new SkillContent { Id = SkillType.DetectingHidden, GainFactor = 1.0, Delay = 30 }
        );
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
        _container.RegisterInstance<ITargetService>(_targets);
        _container.RegisterInstance<ITooltipService>(TestTooltips.Create(_items, _fixture.Mobiles));
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
        _container.RegisterScriptEnum<BodyType>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<SkillModule>();
        _container.AddScriptModule<TargetModule>();
        _container.AddScriptModule<WorldModule>();
        _container.Register<IItemHandlingService, ItemHandlingService>(Reuse.Singleton);
        _container.RegisterDelegate<IScriptEngine>(_ => _engine);
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );

        foreach (var script in new[] { "anatomy", "evaluating_intelligence", "forensic_evaluation", "detecting_hidden" })
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
        var skillScripts = new SkillScriptService(_engine, _loop, options);
        await skillScripts.StartAsync();
        _use = new(_fixture.Mobiles, skillScripts, _speech, _time, data);
    }

    [Fact]
    public void Anatomy_AtTheMost_ReadsTheStrengthTheDexterityAndTheEndurance()
    {
        Skills((SkillType.Anatomy, 1000));
        _targets.Result = TargetResult.ForObject(_bruno.Id);

        Use(SkillType.Anatomy);

        // 100 strength and 50 dexterity: 10 and 5; half of the stamina: 5. No margin of error at 100 points.
        Assert.Empty(_errors);
        Assert.Equal([Whom, 1038045 + 10 * 11 + 5, 1038303 + 5], Told(_aria));
    }

    [Fact]
    public void Anatomy_WithNoSkill_CannotGetASense_AndAskedOfOneself_SaysItKnowsItselfWell()
    {
        _targets.Result = TargetResult.ForObject(_bruno.Id);
        Use(SkillType.Anatomy);

        _time.Advance(TimeSpan.FromSeconds(5));
        _targets.Result = TargetResult.ForObject(_aria.Id);
        Use(SkillType.Anatomy);

        _time.Advance(TimeSpan.FromSeconds(5));
        _targets.Result = TargetResult.ForObject(new Serial(0x40000999));
        Use(SkillType.Anatomy);

        Assert.Empty(_errors);
        Assert.Equal([Whom, CanNotSense, Whom, Yourself, Whom, NotAlive], Told(_aria));
    }

    [Fact]
    public void EvaluatingIntelligence_AtTheMost_ReadsTheIntelligenceAndTheMana_OfWhoIsNotAHumanBody()
    {
        Skills((SkillType.EvaluatingIntelligence, 1200));
        _targets.Result = TargetResult.ForObject(_bruno.Id);

        Use(SkillType.EvaluatingIntelligence);

        // 100 intelligence is 10, half the mana is 5, and a body the data does not list as human is "it", 22.
        Assert.Empty(_errors);
        Assert.Equal([What, 1038169 + 10 + 22, 1038202 + 5], Told(_aria));
    }

    [Fact]
    public void EvaluatingIntelligence_WithNoSkill_CannotJudge_AndOfOneself_SaysItLooksSilly()
    {
        _targets.Result = TargetResult.ForObject(_bruno.Id);
        Use(SkillType.EvaluatingIntelligence);

        _time.Advance(TimeSpan.FromSeconds(5));
        _targets.Result = TargetResult.ForObject(_aria.Id);
        Use(SkillType.EvaluatingIntelligence);

        Assert.Empty(_errors);
        Assert.Equal([What, 1038166 + 2, What, Silly], Told(_aria));
    }

    [Fact]
    public void Forensics_OnTheCorpseOfAPlayer_TellsWhoKilledIt_AndOnOneWithNoKiller_NoOne()
    {
        Skills((SkillType.ForensicEvaluation, 1000));
        var corpse = Corpse(0x40000200, killer: _bruno.Id.Value);
        _targets.Result = TargetResult.ForObject(corpse.Id);
        Use(SkillType.ForensicEvaluation);

        _time.Advance(TimeSpan.FromSeconds(5));
        var found = Corpse(0x40000201, killer: null);
        _targets.Result = TargetResult.ForObject(found.Id);
        Use(SkillType.ForensicEvaluation);

        Assert.Empty(_errors);
        Assert.Equal([ShowMe, KilledBy, ShowMe, KilledBy], Told(_aria));
        Assert.Equal([_bruno.Name, "no one"], _speech.ToldClilocs.Where(told => told.Cliloc == KilledBy).Select(told => told.Arguments));
    }

    [Fact]
    public void Forensics_WithNoSkill_CannotDetermineAnything()
    {
        var corpse = Corpse(0x40000200, killer: _bruno.Id.Value);
        _targets.Result = TargetResult.ForObject(corpse.Id);

        Use(SkillType.ForensicEvaluation);

        Assert.Empty(_errors);
        Assert.Equal([ShowMe, NothingUseful], Told(_aria));
    }

    [Fact]
    public void DetectingHidden_FindsAHiderOfLessSkill_ShowsIt_AndTellsIt()
    {
        Skills((SkillType.DetectingHidden, 1000));
        _bruno.Hidden = true;
        var there = _bruno.Location;
        _targets.Result = TargetResult.ForLocation(_aria.Map, there);

        Use(SkillType.DetectingHidden);

        Assert.Empty(_errors);
        Assert.False(_bruno.Hidden);
        Assert.Equal([Revealed], Told(_bruno));
        Assert.Equal([Where], Told(_aria));
    }

    [Fact]
    public void DetectingHidden_WhenNoOneIsThere_SaysThereIsNothing()
    {
        Skills((SkillType.DetectingHidden, 1000));
        _targets.Result = TargetResult.ForLocation(_aria.Map, _aria.Location);

        Use(SkillType.DetectingHidden);

        Assert.Empty(_errors);
        Assert.Equal([Where, NothingHidden], Told(_aria));
    }

    [Fact]
    public void DetectingHidden_OutOfTheSkillsRange_FindsNoOne()
    {
        // 20 points: two tiles around the place looked at, and half of that when the check fails, as it does here.
        Skills((SkillType.DetectingHidden, 200));
        _bruno.Hidden = true;
        _bruno.Location = new Point3D(_aria.Location.X + 6, _aria.Location.Y, _aria.Location.Z);
        _targets.Result = TargetResult.ForLocation(_aria.Map, _aria.Location);

        Use(SkillType.DetectingHidden);

        Assert.True(_bruno.Hidden);
        Assert.Equal([Where, NothingHidden], Told(_aria));
    }

    [Fact]
    public void DetectingHidden_WithTooLittleSkillToReachATile_LooksAtNone_NotEvenThePickedOne()
    {
        // 9 points: no tile; a hider on the very place picked stays hidden.
        Skills((SkillType.DetectingHidden, 90));
        _bruno.Hidden = true;
        _targets.Result = TargetResult.ForLocation(_aria.Map, _bruno.Location);

        Use(SkillType.DetectingHidden);

        Assert.True(_bruno.Hidden);
        Assert.Equal([Where, NothingHidden], Told(_aria));
    }

    [Fact]
    public void Forensics_OnTheCorpseOfAKillerThatIsGone_StillNamesIt_AndOnANonHumanCorpse_FindsNothingUnusual()
    {
        Skills((SkillType.ForensicEvaluation, 1000));
        var corpse = Corpse(0x40000200, killer: 0x40000777);
        corpse.SetProp("corpse.killer_name", "Lord Blackthorn");
        _targets.Result = TargetResult.ForObject(corpse.Id);
        Use(SkillType.ForensicEvaluation);

        _time.Advance(TimeSpan.FromSeconds(5));
        var beast = Corpse(0x40000201, killer: null);
        beast.SetProp("corpse.owner", null);
        beast.SetProp("corpse.body", 0x11);
        _targets.Result = TargetResult.ForObject(beast.Id);
        Use(SkillType.ForensicEvaluation);

        Assert.Empty(_errors);
        Assert.Equal([ShowMe, KilledBy, ShowMe, 501003], Told(_aria));
        Assert.Equal("Lord Blackthorn", _speech.ToldClilocs.Single(told => told.Cliloc == KilledBy).Arguments);
    }

    [Fact]
    public void EvaluatingIntelligence_OnAnItem_SaysItIsSmarterThanARock()
    {
        _targets.Result = TargetResult.ForObject(new Serial(0x40000999));

        Use(SkillType.EvaluatingIntelligence);

        Assert.Empty(_errors);
        Assert.Equal([What, 500908], Told(_aria));
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
        // The skill check reads the skills of the mobile, the scripts read them from the state service.
        _state.Skills.Clear();
        _state.Skills.AddRange(skills.Select(skill => new MobileSkill { Skill = skill.Skill, Base = skill.Tenths }));
        _aria.Skills = skills.Select(skill => new MobileSkill { Skill = skill.Skill, Base = skill.Tenths }).ToList();
    }

    private ItemEntity Corpse(uint serial, long? killer)
    {
        var corpse = new ItemEntity { Id = new Serial(serial), TemplateId = "corpse", ItemId = 0x2006, Amount = 1 };
        corpse.PlaceOnGround(_aria.Map, new Point3D(_aria.Location.X + 1, _aria.Location.Y, _aria.Location.Z));
        corpse.SetProp("corpse.owner", (long)_bruno.Id.Value);

        if (killer is { } who)
        {
            corpse.SetProp("corpse.killer", who);
        }

        _items.Add([corpse]);

        return corpse;
    }

    private void Use(SkillType skill)
    {
        _loop.DeferTryPost = true;
        _use.Use(_session, skill);

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
