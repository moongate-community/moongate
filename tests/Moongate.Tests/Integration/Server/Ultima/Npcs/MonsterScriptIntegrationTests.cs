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
///     The shipped <c>scripts/mobiles/monster.lua</c> on the real Lua engine and the real modules: a skeleton two a
///     second thinks, a player five tiles east of it.
/// </summary>
public sealed class MonsterScriptIntegrationTests : IAsyncLifetime
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
            new MobileTemplate
            {
                Id = "skeleton", ScriptId = "monster",
                Sounds = new MobileSounds { StartAttack = StartAttackSound, Idle = IdleSound, Attack = AttackSound }
            },
            new MobileTemplate { Id = "lich", ScriptId = "monster", FleeAt = -1 }
        )
    );
    private readonly MobileEntity _skeleton = new()
    {
        Id = new Serial(0x100), Name = "a skeleton", TemplateId = "skeleton", Body = 0x32, Map = MapType.Trammel,
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
        _fixture.Mobiles.EnterWorld(_skeleton);
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
            new StubDataLoaderService().With(new BodyContent { Body = new(0x32), Type = BodyType.Monster }, new BodyContent { Body = new(0x190), Type = BodyType.Human })
        );
        _container.Resolve<IMoongateEventBus>()
                  .Subscribe<ScriptErrorEvent>(
                      (evt, _) =>
                      {
                          _errors.Add(evt);

                          return Task.CompletedTask;
                      }
                  );
        _scripts.Write("common/creature.lua", File.ReadAllText(ShippedScript("common/creature.lua")));
        _scripts.Write("mobiles/monster.lua", File.ReadAllText(ShippedScript("mobiles/monster.lua")));
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
    public void APlayerInSight_IsThreatened_WalkedTo_AndFoughtFromBesideIt()
    {
        _finder.Finds(DirectionType.East, DirectionType.East, DirectionType.East, DirectionType.East);

        // It looks every fourth think.
        Think(3);
        Assert.Empty(_state.Flags);
        Think(1);
        Assert.Empty(_errors.Select(error => error.ToString()));

        Assert.Equal(["war 256 True"], _state.Flags);
        Assert.Contains((_skeleton, StartAttackSound), _speech.Sounds);
        Assert.Contains("Animated 256 11 5 1", _view.Calls);
        Assert.Equal(new Point3D(1600, 1600, 0), _skeleton.Location);

        // One step a think, never running, until it stands beside the player.
        Think(4);
        Assert.Equal(new Point3D(1604, 1600, 0), _skeleton.Location);
        Assert.DoesNotContain(_view.Calls, call => call.Contains("running", StringComparison.OrdinalIgnoreCase));

        // Beside it, it stays, faces the player and fights it: told once, whatever the thinks after.
        Think(12);
        Assert.Equal(new Point3D(1604, 1600, 0), _skeleton.Location);
        Assert.Equal(DirectionType.East, _skeleton.Direction);
        Assert.Equal([(_skeleton, _aria)], _combat.Attacks);
        Assert.Empty(_errors);
    }

    [Fact]
    public void ABlueNpcInSight_IsGoneForToo_WithNoPlayerAround()
    {
        _aria.Hidden = true;
        _finder.Finds(DirectionType.East, DirectionType.East, DirectionType.East, DirectionType.East);
        var townsman = Npc(0x200, 1605, 1600, NotorietyType.Innocent);

        Think(4);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal(["war 256 True"], _state.Flags);

        // One step a think until it stands beside the townsman, and then it fights him.
        Think(4);
        Assert.Equal(new Point3D(1604, 1600, 0), _skeleton.Location);
        Think(12);
        Assert.Equal([(_skeleton, townsman)], _combat.Attacks);
    }

    [Theory]
    [InlineData(NotorietyType.Invulnerable)]
    [InlineData(NotorietyType.Attackable)]
    [InlineData(NotorietyType.Enemy)]
    [InlineData(NotorietyType.Murderer)]
    public void AnNpcThatIsNoBlueTownsman_IsLeftAlone(NotorietyType notoriety)
    {
        _aria.Hidden = true;
        Npc(0x200, 1605, 1600, notoriety);

        Think(40);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Empty(_state.Flags);
        Assert.Empty(_combat.Attacks);
    }

    [Fact]
    public void ADeadPlayer_AGhost_IsNotPreyEither_AndTheBodyOfAHumanDoesNotPlayMonsterActions()
    {
        _skeleton.Body = 0x0190;
        _finder.Finds(DirectionType.East, DirectionType.East, DirectionType.East, DirectionType.East);

        Think(4);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal(["war 256 True"], _state.Flags);
        Assert.DoesNotContain(_view.Calls, call => call.StartsWith("Animated", StringComparison.Ordinal));
    }

    [Fact]
    public void AnArcher_ShootsFromWhereItsBowReachesAndItSeesItsPrey_WithoutWalkingUpToIt()
    {
        _combat.Range = 8;

        Think(4);
        Think(2);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.Equal([(_skeleton, _aria)], _combat.Attacks);
        Assert.Equal(new Point3D(1600, 1600, 0), _skeleton.Location);
        Assert.Equal(DirectionType.East, _skeleton.Direction);
    }

    [Fact]
    public void AnArcherOutOfRange_WalksUntilItsPreyIsInside_ThenShoots()
    {
        _combat.Range = 8;
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1612, 1600, 0)));
        _finder.Finds(Enumerable.Repeat(DirectionType.East, 12).ToArray());

        // Seen at 12 cells, a step each think until the prey is within 7: then it stands and shoots.
        Think(4 + 12);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.InRange(_skeleton.Location.X, 1604, 1606);
        Assert.Equal([(_skeleton, _aria)], _combat.Attacks);
    }

    [Fact]
    public void AnArcherInRangeWithNoLineOfSight_ComesCloser_InsteadOfStandingBlind()
    {
        _combat.Range = 8;
        _sight.Allow = false;
        _finder.Finds(Enumerable.Repeat(DirectionType.East, 8).ToArray());

        // It is seen by the scan only with a line of sight: with none it is not seen at all, so it does not start.
        Think(8);

        Assert.Empty(_combat.Attacks);
        Assert.Equal(new Point3D(1600, 1600, 0), _skeleton.Location);
    }

    [Fact]
    public void AMonsterThatFightsAndIsHurt_NowAndThenRuns_AndDoesNotAnswerTheBlowWhileItDoes()
    {
        _skeleton.Hits = 10;
        _skeleton.HitsMax = 100;
        _combat.Attack(_skeleton, _aria);

        // One chance in ten at each think: a few hundred thinks always have one.
        for (var think = 0; think < 400 && !_skeleton.GetProp<bool>("combat.passive"); think++)
        {
            Think(1);
        }

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.True(_skeleton.GetProp<bool>("combat.passive"));
        Assert.Contains(_skeleton, _combat.Stopped);
        Assert.Contains("war 256 False", _state.Flags);
    }

    [Fact]
    public void AMonsterThatIsUnhurt_OrWhoseTemplateNeverFlees_DoesNotRun()
    {
        _combat.Attack(_skeleton, _aria);
        _skeleton.Hits = 100;
        _skeleton.HitsMax = 100;
        Think(200);
        Assert.False(_skeleton.GetProp<bool>("combat.passive"));

        _skeleton.TemplateId = "lich";
        _skeleton.Hits = 1;
        Think(400);

        Assert.Empty(_errors.Select(error => error.ToString()));
        Assert.False(_skeleton.GetProp<bool>("combat.passive"));
    }

    [Fact]
    public async Task AHiddenPlayer_AGameMaster_OrOneOutOfSight_IsLeftAlone()
    {
        _aria.Hidden = true;
        Think(8);
        _aria.Hidden = false;

        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.GameMaster));
        Think(8);
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.Regular));

        _sight.Allow = false;
        Think(8);

        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1617, 1600, 0)));
        _sight.Allow = true;
        Think(8);

        Assert.Empty(_state.Flags);
        Assert.Equal(new Point3D(1600, 1600, 0), _skeleton.Location);
        Assert.Empty(_errors);
    }

    [Theory, InlineData(true), InlineData(false)]
    public void APreyThatHidesOrGoesTooFar_IsLost_ItStandsGuardTenSecondsAndGoesBackToPeace(bool hides)
    {
        _finder.Finds(DirectionType.East);
        Think(4);
        Assert.Equal(["war 256 True"], _state.Flags);

        if (hides)
        {
            _aria.Hidden = true;
        }
        else
        {
            Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1640, 1600, 0)));
        }

        // The think that loses it, then twenty of guard, still in war mode.
        Think(20);
        Assert.Equal(["war 256 True"], _state.Flags);

        Think(3);
        Assert.Equal(["war 256 True", "war 256 False"], _state.Flags);
        Assert.Empty(_errors);
    }

    [Fact]
    public void APreyItCannotReach_IsGivenUpAfterTwentySeconds_AndLeftAloneUntilItMoves()
    {
        // No path is ever found.
        Think(4);
        Assert.Equal(["war 256 True"], _state.Flags);

        // Forty stalled thinks, twenty of guard, then peace: it does not start again on a player that stands still.
        Think(40 + 21 + 20);
        Assert.Equal(["war 256 True", "war 256 False"], _state.Flags);

        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1606, 1600, 0)));
        Think(4);
        Assert.Equal(["war 256 True", "war 256 False", "war 256 True"], _state.Flags);
        Assert.Empty(_errors);
    }

    [Fact]
    public void WithNobodyAround_ItStrollsInsideItsHome_AndRestsWithItsIdleSound()
    {
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1700, 1700, 0)));
        SetHome(1598, 1598, 1602, 1602);

        var cells = new HashSet<Point3D>();

        for (var think = 0; think < 600; think++)
        {
            Think(1);
            cells.Add(_skeleton.Location);
            Assert.InRange(_skeleton.Location.X, 1598, 1602);
            Assert.InRange(_skeleton.Location.Y, 1598, 1602);
        }

        // Five minutes of thinks: it moved, and it rested at least once.
        Assert.True(cells.Count > 1);
        Assert.Contains((_skeleton, IdleSound), _speech.Sounds);
        Assert.Empty(_state.Flags);
        Assert.Empty(_errors);
    }

    [Fact]
    public void OutsideItsHome_ItWalksBackToIt()
    {
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1700, 1700, 0)));
        SetHome(1590, 1600, 1592, 1600);
        _finder.Finds(Enumerable.Repeat(DirectionType.West, 8).ToArray());

        // A rest of 25 seconds may come in between: the cap leaves room for many.
        for (var think = 0; think < 2000 && _skeleton.Location.X > 1592; think++)
        {
            Think(1);
        }

        Assert.InRange(_skeleton.Location.X, 1590, 1592);
        Assert.Contains(_finder.Searches, search => search.To == new Point3D(1592, 1600, 0));
        Assert.Empty(_errors);
    }

    [Fact]
    public void OutsideItsHome_WithNoWayBack_ItStillMoves_InsteadOfPushingAgainstAWall()
    {
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1700, 1700, 0)));
        SetHome(1590, 1600, 1592, 1600);
        var cells = new HashSet<Point3D>();

        // No path is ever found.
        for (var think = 0; think < 2000 && cells.Count < 3; think++)
        {
            Think(1);
            cells.Add(_skeleton.Location);
        }

        Assert.True(cells.Count >= 3);
        Assert.Empty(_errors);
    }

    [Fact]
    public void AMonsterStillInWarModeWhenTheScriptForgotIt_GoesBackToPeace()
    {
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1700, 1700, 0)));
        _skeleton.WarMode = true;

        Think(1);

        Assert.Equal(["war 256 False"], _state.Flags);
    }

    [Fact]
    public void APlayerOneTileAwayOnTheFloorAbove_IsNotReached()
    {
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1601, 1600, 20)));

        Think(40);

        Assert.Empty(_combat.Attacks);
        Assert.Empty(_errors);
    }

    [Fact]
    public void AMonsterThatIsFought_WhileItWanders_TurnsOnWhoFightsIt_AndWalksToIt()
    {
        _finder.Finds(DirectionType.East, DirectionType.East, DirectionType.East, DirectionType.East);
        // The combat service made the skeleton fight the player that hit it: no scan of its own has seen it yet.
        _combat.Attack(_skeleton, _aria);

        Think(1);

        Assert.Equal(["war 256 True"], _state.Flags);
        Assert.Equal(new Point3D(1601, 1600, 0), _skeleton.Location);
        Think(3);
        Assert.Equal(new Point3D(1604, 1600, 0), _skeleton.Location);
        Assert.Empty(_errors);
    }

    [Fact]
    public void AMonsterThatIsFought_WhileItStandsGuard_ChasesAgain_WithoutThreatening()
    {
        _finder.Finds(DirectionType.East, DirectionType.East, DirectionType.East, DirectionType.East);
        Think(20);
        _aria.Hidden = true;
        Think(1);
        Assert.Contains(_skeleton, _combat.Stopped);
        _speech.Sounds.Clear();
        _state.Flags.Clear();

        // The player strikes from hiding: the monster fights back, and does not threaten twice.
        _combat.Attack(_skeleton, _aria);
        Think(1);

        Assert.Equal(["war 256 True"], _state.Flags);
        Assert.DoesNotContain((_skeleton, StartAttackSound), _speech.Sounds);
        Assert.Empty(_errors);
    }

    [Fact]
    public void APlayerItLosesOrGivesUp_EndsItsFight()
    {
        _finder.Finds(DirectionType.East, DirectionType.East, DirectionType.East, DirectionType.East);
        Think(20);
        Assert.Single(_combat.Attacks);

        // The player hides: the monster loses it and stands guard, no longer fighting it.
        _aria.Hidden = true;
        Think(1);

        Assert.Contains(_skeleton, _combat.Stopped);
        Assert.Empty(_errors);
    }

    private MobileEntity Npc(uint serial, int x, int y, NotorietyType notoriety)
    {
        var npc = new MobileEntity
        {
            Id = new Serial(serial), Name = "a townsman", TemplateId = "townsman", Notoriety = notoriety, Map = MapType.Trammel,
            Location = new Point3D(x, y, 0), Hits = 20, HitsMax = 20
        };
        _fixture.Mobiles.EnterWorld(npc);

        return npc;
    }

    private void Think(int times)
    {
        for (var think = 0; think < times; think++)
        {
            _npcs.Think(_skeleton);
        }
    }

    private void SetHome(long x1, long y1, long x2, long y2)
    {
        _skeleton.SetProp("spawn.x1", x1);
        _skeleton.SetProp("spawn.y1", y1);
        _skeleton.SetProp("spawn.x2", x2);
        _skeleton.SetProp("spawn.y2", y2);
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
