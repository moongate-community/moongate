using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Effects;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Death;
using Moongate.Tests.TestSupport.Ultima.Effects;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Npcs;

/// <summary>
///     The shipped <c>scripts/mobiles/healer.lua</c>, the gump <c>resurrect</c> it opens and its script, on the real
///     Lua
///     engine and the real modules: a healer at 1600,1600 and a ghost three tiles east of it.
/// </summary>
public sealed class HealerScriptIntegrationTests : IAsyncLifetime
{
    private const int ContinueButton = 1;
    private const int CriminalCliloc = 501222;
    private const int MurdererCliloc = 501223;
    private const int StrayedCliloc = 501224;
    private const int OfferSound = 0x1F2;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly RecordingTeleportService _teleports = new();
    private readonly RecordingEffectService _effects = new();
    private readonly RecordingGumpService _gumps = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly StubDeathService _death = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private readonly MobileTemplateService _templates = new(
        new StubDataLoaderService().With(
            new MobileTemplate { Id = "healer", ScriptId = "healer" },
            new MobileTemplate { Id = "m_whealer", ScriptId = "healer" },
            new MobileTemplate { Id = "evilhealer", ScriptId = "healer" }
        )
    );

    private readonly MobileEntity _healer = new()
    {
        Id = new Serial(0x100), Name = "Brother Ames", TemplateId = "healer", Map = MapType.Trammel,
        Location = new Point3D(1600, 1600, 0), Direction = DirectionType.North
    };

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private LuaScriptEngineService _engine = null!;
    private NpcScriptService _npcs = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.AccountId = new Serial(0x42);
        _aria.Body = 0x0192;
        _aria.Hidden = true;
        _aria.Fame = 1005;
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1603, 1600, 0)));
        _fixture.Mobiles.EnterWorld(_healer);
        var time = new SettableClock();
        GumpScriptService? gumpScripts = null;
        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        var templates =
            (await new GumpsLoader(new DirectoriesConfig(root, ["templates"])).LoadDataAsync()).Entities.ToArray();
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IMobileStateService>(_state);
        _container.RegisterInstance<ITeleportService>(_teleports);
        _container.RegisterInstance<IEffectService>(_effects);
        _container.RegisterInstance<ICrimeService>(new RecordingCrimeService());
        _container.RegisterInstance<IDeathService>(_death);
        _container.RegisterInstance<IMobileTemplateService>(_templates);
        _container.RegisterInstance<ILineOfSightService>(_sight);
        _container.RegisterInstance<IPathfindingService>(new StubPathfindingService());
        _container.RegisterInstance<IMovementService>(new StubMovementService());
        _container.RegisterInstance<IItemService>(TestItems.Create(_fixture.Sectors));
        _container.RegisterInstance<IGumpService>(_gumps);
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With(templates), _loop, _fixture.Sessions)
        );
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
        _container.RegisterInstance<TimeProvider>(time);
        _container.AddScriptModule<NpcModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<EffectModule>();
        _container.AddScriptModule<GumpModule>();
        _container.RegisterScriptEnum<EffectGraphicType>();
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );
        _scripts.Write(
            "mobiles/healer.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "mobiles", "healer.lua"))
        );
        _scripts.Write(
            "common/training.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "common", "training.lua"))
        );
        _scripts.Write(
            "gumps/resurrect.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "gumps", "resurrect.lua"))
        );
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
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
        gumpScripts = new GumpScriptService(_engine, _loop, options);
        await gumpScripts.StartAsync();
        _npcs = new(_engine, _templates, _loop, new ScriptEngineOptions { ScriptsDirectory = _scripts.Path });
        await _npcs.StartAsync();
    }

    [Fact]
    public void AGhostComingNear_IsOfferedTheGump_WithTheSoundAndTheSparkles()
    {
        Think(1);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal("resurrect", Assert.Single(_gumps.Opened).Gump.Id);
        Assert.Equal((_healer, OfferSound), Assert.Single(_speech.Sounds));
        Assert.Equal(0x376A, Assert.Single(_effects.On).Options.Graphic);
    }

    [Fact]
    public async Task TheGhostOfAGameMaster_IsOfferedToo()
    {
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.GameMaster));

        Think(1);

        Assert.Single(_gumps.Opened);
    }

    [Fact]
    public void Continue_OfAGhostOnAnotherMap_RaisesNobody()
    {
        Think(1);
        _aria.Map = MapType.Felucca;

        Answer(ContinueButton);

        Assert.Empty(_death.PlayersRaised);
    }

    [Fact]
    public void AGhostThatStaysNear_IsOfferedOnce()
    {
        Think(6);

        Assert.Single(_gumps.Opened);
    }

    [Fact]
    public void AGhostThatLeavesAndComesBack_IsOfferedAgain_AfterTheTwoSeconds()
    {
        Think(1);
        _aria.Location = new Point3D(1610, 1600, 0);
        Think(1);
        _aria.Location = new Point3D(1603, 1600, 0);

        // The second is not over: the healer waits, and offers when its two seconds are.
        Think(1);
        Assert.Single(_gumps.Opened);
        Think(4);

        Assert.Equal(2, _gumps.Opened.Count);
    }

    [Fact]
    public void AGhostBeyondFourCells_OrOutOfSight_IsNotOffered()
    {
        _aria.Location = new Point3D(1606, 1600, 0);
        Think(2);
        _aria.Location = new Point3D(1603, 1600, 0);
        _sight.Allow = false;
        Think(2);

        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public void ALivingPlayer_IsOfferedNothing()
    {
        _aria.Body = 0x0190;

        Think(2);

        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public void ACriminalGhost_IsRefused_WithoutTheGump()
    {
        _aria.Criminal = true;

        Think(1);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Empty(_gumps.Opened);
        Assert.Equal((_healer, CriminalCliloc, ""), Assert.Single(_speech.SaidClilocs));
    }

    [Fact]
    public void AMurdererGhost_IsRefused_ByAGoodHealer_WithoutTheGump()
    {
        _aria.Kills = 5;

        Think(1);

        Assert.Empty(_gumps.Opened);
        Assert.Equal((_healer, MurdererCliloc, ""), Assert.Single(_speech.SaidClilocs));
    }

    [Fact]
    public void AnEvilHealer_RaisesTheCriminalAndTheMurdererToo_WithoutAWord()
    {
        _healer.TemplateId = "evilhealer";
        _aria.Kills = 5;
        _aria.Criminal = true;
        _aria.Karma = -3000;

        Think(1);

        Assert.Single(_gumps.Opened);
        Assert.Empty(_speech.SaidClilocs);
    }

    [Fact]
    public void Continue_OfAPlayerWithFiveShortTermMurders_CostsItsStatsAndItsSkills_NotBelowTheirFloors()
    {
        _aria.ShortTermMurders = 10;
        _aria.Strength = 100;
        _aria.Dexterity = 10;
        _aria.Intelligence = 50;
        _state.Skills.Add(new() { Skill = SkillType.Magery, Base = 800 });
        _state.Skills.Add(new() { Skill = SkillType.Tactics, Base = 360 });
        Think(1);

        Answer(ContinueButton);

        Assert.Empty(_errors.Select(error => error.Error.Message));
        // Ten murders keep 94%: 94 strength, 47 intelligence; a stat that would fall under 10 is kept, as a skill under 35.
        Assert.Equal((94, 10, 47), (_aria.Strength, _aria.Dexterity, _aria.Intelligence));
        Assert.Equal([(_aria, SkillType.Magery, 752, (int?)null)], _state.SkillsSet);
    }

    [Fact]
    public void Continue_OfAPlayerWithFewMurders_KeepsItsStatsAndSkills()
    {
        _aria.ShortTermMurders = 4;
        _aria.Strength = 100;
        _state.Skills.Add(new() { Skill = SkillType.Magery, Base = 800 });
        Think(1);

        Answer(ContinueButton);

        Assert.Equal(100, _aria.Strength);
        Assert.Empty(_state.SkillsSet);
    }

    [Fact]
    public void AGhostOfNegativeKarma_IsToldItStrayed_AndOfferedAllTheSame()
    {
        _aria.Karma = -50;

        Think(1);

        Assert.Equal((_healer, StrayedCliloc, ""), Assert.Single(_speech.SaidClilocs));
        Assert.Single(_gumps.Opened);
    }

    [Fact]
    public void Continue_RaisesTheGhost_WithTheSoundAndTheSparkles_AndTakesATenthOfItsFame()
    {
        Think(1);
        _speech.Sounds.Clear();
        _effects.On.Clear();

        Answer(ContinueButton);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal([_aria], _death.PlayersRaised);
        Assert.Equal((_aria, 0x214), Assert.Single(_speech.Sounds));
        Assert.Equal(0x376A, Assert.Single(_effects.On).Options.Graphic);
        Assert.Equal(1005 - 100, _aria.Fame);
    }

    [Fact]
    public void Continue_AfterWalkingFarFromTheHealer_RaisesNobody()
    {
        Think(1);
        _aria.Location = new Point3D(1620, 1600, 0);

        Answer(ContinueButton);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Empty(_death.PlayersRaised);
        Assert.Equal(1005, _aria.Fame);
    }

    [Fact]
    public void AWanderingHealer_StrollsWithoutAnError_AndStillOffers()
    {
        _healer.TemplateId = "m_whealer";

        Think(4);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Single(_gumps.Opened);
    }

    private void Think(int times)
    {
        for (var think = 0; think < times; think++)
        {
            _npcs.Think(_healer);
        }
    }

    private void Answer(int button)
    {
        _gumps.Opened[0]
            .Gump.OnResponse(
                _session,
                new GumpResponse { ButtonId = button, Switches = new HashSet<int>(), Texts = new Dictionary<int, string>() }
            );
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

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }
}
