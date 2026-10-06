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
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Combat;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Npcs;

/// <summary>
///     The shipped <c>scripts/mobiles/animal.lua</c> and <c>scripts/mobiles/scared_animal.lua</c> on the real Lua
///     engine
///     and the real modules: an animal two a second thinks, a player five tiles east of it.
/// </summary>
public sealed class AnimalScriptIntegrationTests : IAsyncLifetime
{
    private const int StartAttackSound = 451;
    private const int IdleSound = 452;
    private const int AttackSound = 453;

    private readonly RecordingCombatService _combat = new();
    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingMobileStateService _state = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly StubPathfindingService _finder = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private readonly MobileTemplateService _templates = new(
        new StubDataLoaderService().With(
            new MobileTemplate { Id = "bear", ScriptId = "animal", Sounds = new MobileSounds { Idle = IdleSound } },
            new MobileTemplate { Id = "rabbit", ScriptId = "scared_animal" }
        )
    );

    private readonly MobileEntity _animal = new()
    {
        Id = new Serial(0x100), Name = "a bear", TemplateId = "bear", Body = 0xD3, Map = MapType.Trammel,
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
        _fixture.Mobiles.EnterWorld(_animal);
        // A home of one cell: the skeleton does not stroll, so its steps are those of the chase only.
        SetHome(1600, 1600, 1600, 1600);
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
        _container.RegisterInstance<ITeleportService>(new RecordingTeleportService());
        _container.RegisterInstance<IMobileTemplateService>(_templates);
        _container.RegisterInstance<ILineOfSightService>(_sight);
        _container.RegisterInstance<IPathfindingService>(_finder);
        _container.RegisterInstance<INpcPathService>(new NpcPathService(_finder, time));
        _container.RegisterInstance<IMovementService>(new StubMovementService());
        _container.RegisterInstance<IItemService>(TestItems.Create(_fixture.Sectors));
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<TimeProvider>(time);
        _container.AddScriptModule<NpcModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<WorldModule>();
        _container.AddScriptModule<DiceModule>();
        _container.AddScriptModule<CombatModule>();
        _container.RegisterInstance<ICombatService>(_combat);
        _container.RegisterScriptEnum<MonsterAnimationType>();
        _container.RegisterScriptEnum<BodyType>();
        _container.RegisterInstance<IDataLoaderService>(
            new StubDataLoaderService().With(new BodyContent { Body = new(0xD3), Type = BodyType.Animal })
        );
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );
        _scripts.Write("common/creature.lua", File.ReadAllText(ShippedScript("common/creature.lua")));
        _scripts.Write("common/creature.lua", File.ReadAllText(ShippedScript("common/creature.lua")));
        _scripts.Write("mobiles/animal.lua", File.ReadAllText(ShippedScript("mobiles/animal.lua")));
        _scripts.Write("mobiles/scared_animal.lua", File.ReadAllText(ShippedScript("mobiles/scared_animal.lua")));
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path,
            MaxInstructionsPerResume = 20_000,
            MaxInstructionsPerChunk = 100_000,
            HookInterval = 100,
            WriteDefinitions = false
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
        _npcs = new(_engine, _templates, _loop, new ScriptEngineOptions { ScriptsDirectory = _scripts.Path });
        await _npcs.StartAsync();
    }

    [Fact]
    public void AnAnimalThatSeesAPlayer_LeavesItAlone_ItNeverStartsAFight()
    {
        Think(40);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Empty(_state.Flags);
        Assert.Empty(_combat.Attacks);
    }

    [Fact]
    public void AnAnimalThatIsFought_TurnsOnWhoFightsIt_AndWalksToIt()
    {
        _finder.Finds(DirectionType.East, DirectionType.East, DirectionType.East, DirectionType.East);
        _combat.Attack(_animal, _aria);

        Think(4);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal(["war 256 True"], _state.Flags);
        Assert.Equal(new Point3D(1604, 1600, 0), _animal.Location);
    }

    [Fact]
    public void AScaredAnimalThatIsHit_StopsFighting_AndRunsFromWhoHitIt_ThenStrollsAgain()
    {
        _animal.TemplateId = "rabbit";
        _finder.Finds(Enumerable.Repeat(DirectionType.West, 40).ToArray());
        _combat.Attack(_animal, _aria);

        Think(1);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Contains(_animal, _combat.Stopped);
        Assert.Equal(new Point3D(1599, 1600, 0), _animal.Location);

        // It keeps running while the combat service makes it answer the blow again: it never fights.
        _combat.Stopped.Clear();
        Think(5);
        Assert.Contains(_animal, _combat.Stopped);
        Assert.True(_animal.Location.X < 1599);

        // Ten seconds on, or far enough, it is at peace again.
        Think(20);
        Assert.Contains("war 256 False", _state.Flags);
    }

    [Fact]
    public void AScaredAnimalThatSeesAPlayerNobodyHit_StrollsAsIfNothingHappened()
    {
        _animal.TemplateId = "rabbit";

        Think(40);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Empty(_combat.Stopped);
        Assert.Empty(_state.Flags);
    }

    private void Think(int times)
    {
        for (var think = 0; think < times; think++)
        {
            _npcs.Think(_animal);
        }
    }

    private void SetHome(long x1, long y1, long x2, long y2)
    {
        _animal.SetProp("spawn.x1", x1);
        _animal.SetProp("spawn.y1", y1);
        _animal.SetProp("spawn.x2", x2);
        _animal.SetProp("spawn.y2", y2);
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

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }
}
