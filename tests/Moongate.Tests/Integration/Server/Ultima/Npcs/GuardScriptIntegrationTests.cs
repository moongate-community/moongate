using DryIoc;
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
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Effects;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Effects;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Npcs;

/// <summary>
///     The shipped <c>scripts/mobiles/guard.lua</c> on the real Lua engine and the real modules: a guard in a guarded
///     town thinks two a second, a player five tiles east of it.
/// </summary>
public sealed class GuardScriptIntegrationTests : IAsyncLifetime
{
    private const int TeleportSound = 0x1FE;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingMobileStateService _state = new();
    private readonly RecordingTeleportService _teleports = new();
    private readonly RecordingEffectService _effects = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly StubPathfindingService _finder = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly MobileTemplateService _templates = new(
        new StubDataLoaderService().With(new MobileTemplate { Id = "guard", ScriptId = "guard" })
    );
    private readonly MobileEntity _guard = new()
    {
        Id = new Serial(0x100), Name = "a guard", TemplateId = "guard", Map = MapType.Trammel,
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
        // A player's character: what tells it from an NPC.
        _aria.AccountId = new Serial(0x42);
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1605, 1600, 0)));
        _fixture.Mobiles.EnterWorld(_guard);
        // A post of one cell: the guard does not stroll, so its steps are those of the arrest only.
        SetPost(1600, 1600, 1600, 1600);
        var time = new SettableClock();
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IMobileStateService>(_state);
        _container.RegisterInstance<ITeleportService>(_teleports);
        _container.RegisterInstance<IEffectService>(_effects);
        _container.RegisterInstance<ICrimeService>(new RecordingCrimeService());
        _container.RegisterInstance<IMobileTemplateService>(_templates);
        _container.RegisterInstance<ILineOfSightService>(_sight);
        _container.RegisterInstance<IPathfindingService>(_finder);
        _container.RegisterInstance<INpcPathService>(new NpcPathService(_finder, time));
        _container.RegisterInstance<IMovementService>(new StubMovementService());
        _container.RegisterInstance<IItemService>(TestItems.Create(_fixture.Sectors));
        _container.RegisterInstance<IClockService>(new StubClockService());
        // The town: guarded up to x 1650, the wilderness beyond.
        _container.RegisterInstance<IRegionService>(
            new RegionService(
                new StubDataLoaderService().With(
                    new RegionContent
                    {
                        Map = MapType.Trammel, Name = "Britain", Guarded = true,
                        Areas = [new() { X1 = 1500, Y1 = 1500, X2 = 1650, Y2 = 1700 }]
                    }
                )
            )
        );
        _container.RegisterInstance(TestLocalization.With((30138, "Ti pentirai delle tue azioni, canaglia!")));
        _container.RegisterInstance<TimeProvider>(time);
        _container.AddScriptModule<NpcModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<WorldModule>();
        _container.AddScriptModule<EffectModule>();
        _container.AddScriptModule<LocalizationModule>();
        _container.RegisterScriptEnum<EffectGraphicType>();
        _container.Resolve<IMoongateEventBus>()
                  .Subscribe<ScriptErrorEvent>(
                      (evt, _) =>
                      {
                          _errors.Add(evt);

                          return Task.CompletedTask;
                      }
                  );
        _scripts.Write("mobiles/guard.lua", File.ReadAllText(ShippedScript("mobiles/guard.lua")));
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path,
            MaxInstructionsPerResume = 20_000,
            MaxInstructionsPerChunk = 100_000,
            HookInterval = 100,
            WriteDefinitions = false
        };
        _engine = new(options, _container.Resolve<IScriptModuleRegistry>(), _container, _loop, _timers, new EventBusAdapter(_container));
        await _engine.StartAsync();
        _npcs = new(_engine, _templates, _loop, new ScriptEngineOptions { ScriptsDirectory = _scripts.Path });
        await _npcs.StartAsync();
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void AnInnocentInSight_IsLeftAlone()
    {
        Think(8);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Empty(_state.Flags);
        Assert.Empty(_teleports.Teleports);
        Assert.Empty(_speech.Said);
    }

    [Fact]
    public void ACriminalInSight_GetsTheGuardOnIt_WithItsEffectSoundAndLine()
    {
        _aria.Criminal = true;

        // It looks every second think.
        Think(1);
        Assert.Empty(_state.Flags);
        Think(1);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal(["war 256 True"], _state.Flags);
        Assert.Equal((_guard, MapType.Trammel, new Point3D(1605, 1600, 0)), Assert.Single(_teleports.Teleports));
        // A puff where it stood and one where it comes.
        Assert.Equal([new Point3D(1600, 1600, 0), new Point3D(1605, 1600, 0)], _effects.At.Select(effect => effect.Location));
        Assert.All(_effects.At, effect => Assert.Equal((int)EffectGraphicType.Smoke, effect.Options.Graphic));
        Assert.Contains((_guard, TeleportSound), _speech.Sounds);
        Assert.Equal((_guard, "Ti pentirai delle tue azioni, canaglia!"), Assert.Single(_speech.Said));
    }

    [Fact]
    public void BesideItsCriminal_ItFacesIt_AndRunsAfterItWhenItMoves()
    {
        _aria.Criminal = true;
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1601, 1600, 0)));

        Think(4);

        // Already beside it: no teleport, it turns to it.
        Assert.Empty(_teleports.Teleports);
        Assert.Equal(DirectionType.East, _guard.Direction);
        Assert.Single(_speech.Said);

        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1605, 1600, 0)));
        _finder.Finds(DirectionType.East, DirectionType.East, DirectionType.East);
        Think(3);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal(new Point3D(1603, 1600, 0), _guard.Location);
        Assert.Contains(_view.Calls, call => call.EndsWith(" run", StringComparison.Ordinal));
        // It says its line once.
        Assert.Single(_speech.Said);
    }

    [Theory, InlineData("pardoned"), InlineData("hidden"), InlineData("out of town"), InlineData("too far")]
    public void ACriminalItLoses_IsLetGo_AndTheGuardGoesBackToPeace(string how)
    {
        _aria.Criminal = true;
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1601, 1600, 0)));
        Think(2);
        Assert.Equal(["war 256 True"], _state.Flags);

        switch (how)
        {
            case "pardoned":
                _aria.Criminal = false;

                break;
            case "hidden":
                _aria.Hidden = true;

                break;
            case "out of town":
                Assert.True(_fixture.Mobiles.MoveTo(_guard, MapType.Trammel, new Point3D(1649, 1600, 0)));
                Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1651, 1600, 0)));

                break;
            default:
                Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1626, 1600, 0)));

                break;
        }

        Think(1);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal(["war 256 True", "war 256 False"], _state.Flags);
    }

    [Fact]
    public async Task ACriminalOutsideAGuardedRegion_AHiddenOne_OrTheStaff_IsNotArrested()
    {
        _aria.Criminal = true;
        Assert.True(_fixture.Mobiles.MoveTo(_guard, MapType.Trammel, new Point3D(1649, 1600, 0)));
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1652, 1600, 0)));
        Think(4);
        Assert.Empty(_state.Flags);

        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1645, 1600, 0)));
        _aria.Hidden = true;
        Think(4);
        Assert.Empty(_state.Flags);

        _aria.Hidden = false;
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.GameMaster));
        Think(4);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Empty(_state.Flags);
    }

    [Fact]
    public void AGuardThatWasCalled_StaysOnItsCriminal_WithoutSayingItsLineAgain_AndDoesNotStroll()
    {
        _guard.SetProp("guard.summoned", true);
        _aria.Criminal = true;
        Assert.True(_fixture.Mobiles.MoveTo(_guard, MapType.Trammel, new Point3D(1605, 1600, 0)));

        Think(2);

        Assert.Equal(["war 256 True"], _state.Flags);
        Assert.Empty(_speech.Said);
        Assert.Empty(_teleports.Teleports);

        // Its criminal pardoned, it stands where it came until the server sends it away.
        _aria.Criminal = false;
        SetPost(1500, 1500, 1640, 1640);
        Think(60);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal(new Point3D(1605, 1600, 0), _guard.Location);
    }

    [Fact]
    public void WithNobodyToArrest_AStandingGuardStrollsInsideItsPost()
    {
        SetPost(1595, 1595, 1605, 1605);

        Think(200);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.NotEqual(new Point3D(1600, 1600, 0), _guard.Location);
        Assert.InRange(_guard.Location.X, 1595, 1605);
        Assert.InRange(_guard.Location.Y, 1595, 1605);
    }

    private void Think(int times)
    {
        for (var think = 0; think < times; think++)
        {
            _npcs.Think(_guard);
        }
    }

    private void SetPost(long x1, long y1, long x2, long y2)
    {
        _guard.SetProp("spawn.x1", x1);
        _guard.SetProp("spawn.y1", y1);
        _guard.SetProp("spawn.x2", x2);
        _guard.SetProp("spawn.y2", y2);
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
