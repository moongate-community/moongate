using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Effects;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class GuardServiceTests : IAsyncLifetime
{
    private static readonly int[] GuardsKeyword = [0x0007];

    private readonly RecordingTimerService _timers = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingEffectService _effects = new();
    private readonly StubNpcService _npcs = new()
    {
        Spawned = new() { Id = new Serial(0x200), Name = "a guard", TemplateId = "guard" }
    };
    private readonly CrimeConfig _config = new();
    private readonly SettableClock _clock = new();
    private readonly StubMovementService _movement = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _thiefSession = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _thief = null!;
    private GuardService _guards = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        _thiefSession = await _fixture.AddAsync(3);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        Assert.True(_fixture.Mobiles.TryGet(new Serial(3), out _thief!));
        Move(_aria, 50, 50);
        Move(_thief, 60, 50);
        _thief.Criminal = true;
        var regions = new RegionService(
            new StubDataLoaderService().With(
                new RegionContent
                {
                    Map = MapType.Trammel, Name = "Britain", Guarded = true,
                    Areas = [new() { X1 = 0, Y1 = 0, X2 = 100, Y2 = 100 }]
                }
            )
        );
        _guards = new(
            _timers,
            _npcs,
            _fixture.Mobiles,
            _fixture.Sectors,
            regions,
            _fixture.Sessions,
            _speech,
            _effects,
            _fixture.Network.Loop,
            _config,
            _clock,
            movement: _movement
        );
        // As the real service: a spawn or a removal started on the loop thread throws.
        _npcs.OnLoopThread = () => _fixture.Network.Loop.IsOnLoopThread;
        await _guards.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _guards.StopAsync();
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void StartAsync_RegistersOneRepeatingTimerEverySecond()
    {
        var timer = Assert.Single(_timers.Timers);

        Assert.Equal(("guards", TimeSpan.FromSeconds(1), true), (timer.Name, timer.Interval, timer.Repeat));
    }

    [Fact]
    public async Task AGuard_ComesForAMurdererToo_WhoseNameIsRed()
    {
        _thief.Criminal = false;
        _thief.Kills = 5;

        await HeardAsync("guards", GuardsKeyword);

        Assert.Single(_npcs.Spawns);
    }

    [Fact]
    public async Task AGuard_ComesOntoTheCriminal_WhenNoTileAroundItCanBeSteppedOn()
    {
        // Walled in on every side.
        _movement.Allow = false;

        await HeardAsync("qualcosa", GuardsKeyword);

        Assert.Equal(new Point3D(60, 50, 0), Assert.Single(_npcs.Spawns).Location);
    }

    [Fact]
    public async Task AGuard_ComesOntoTheOnlyTileAroundTheCriminalThatNobodyStandsOn()
    {
        // Someone on seven of the eight tiles around the thief: the one to the north-west is left.
        var serial = 0x300u;

        await _fixture.Network.ExecuteOnLoopAsync(
            () =>
            {
                for (var x = 59; x <= 61; x++)
                {
                    for (var y = 49; y <= 51; y++)
                    {
                        if ((x, y) is not ((60, 50) or (59, 49)))
                        {
                            _fixture.Mobiles.EnterWorld(
                                new MobileEntity { Id = new Serial(serial++), Name = "a bystander", Map = MapType.Trammel, Location = new Point3D(x, y, 0) }
                            );
                        }
                    }
                }
            }
        );

        await HeardAsync("qualcosa", GuardsKeyword);

        Assert.Equal(new Point3D(59, 49, 0), Assert.Single(_npcs.Spawns).Location);
    }

    [Fact]
    public async Task TheGuardsKeyword_InAGuardedRegion_BringsAGuardBesideTheCriminal_WithItsEffectSoundAndLine()
    {
        await HeardAsync("qualcosa", GuardsKeyword);

        // Beside the criminal, a step away, not on it.
        var spawn = Assert.Single(_npcs.Spawns);
        Assert.Equal(("guard", MapType.Trammel), (spawn.TemplateId, spawn.Map));
        Assert.Equal(1, Math.Max(Math.Abs(spawn.Location.X - 60), Math.Abs(spawn.Location.Y - 50)));
        Assert.True(_npcs.Spawned.GetProp("guard.summoned", false));
        // The effect and the sound are where the guard comes.
        var effect = Assert.Single(_effects.At);
        Assert.Equal((MapType.Trammel, spawn.Location, GuardService.TeleportEffect), (effect.Map, effect.Location, effect.Options.Graphic));
        Assert.Equal((MapType.Trammel, spawn.Location, GuardService.TeleportSound), Assert.Single(_speech.PlacedSounds));
        Assert.Equal((_npcs.Spawned, "Thou wilt regret thine actions, swine!"), Assert.Single(_speech.Said));
    }

    [Theory,
     InlineData("guards", true),
     InlineData("Guards! Help!", true),
     InlineData("GUARDS", true),
     InlineData("I love the vanguards of old", false),
     InlineData("hello", false)]
    public async Task ThePlainWord_CallsThemToo_ForAClientThatSendsNoKeyword(string text, bool called)
    {
        await HeardAsync(text);

        Assert.Equal(called ? 1 : 0, _npcs.Spawns.Count);
    }

    [Fact]
    public async Task OutsideAGuardedRegion_OrWithGuardsOff_NobodyComes()
    {
        Move(_aria, 500, 500);
        Move(_thief, 505, 500);
        Assert.Equal(0, await CallAsync());

        Move(_aria, 50, 50);
        Move(_thief, 60, 50);
        _config.GuardsEnabled = false;
        await HeardAsync("guards", GuardsKeyword);

        Assert.Empty(_npcs.Spawns);
    }

    [Fact]
    public async Task OnlyACriminalInRange_ThatIsNotStaff_GetsAGuard()
    {
        _thief.Criminal = false;
        Assert.Equal(0, await CallAsync());

        _thief.Criminal = true;
        Move(_thief, 65, 50);
        Assert.Equal(0, await CallAsync());

        Move(_thief, 64, 50);
        await _fixture.Network.ExecuteOnLoopAsync(() => _thiefSession.Set(SessionKeys.AccountType, AccountType.GameMaster));
        Assert.Equal(0, await CallAsync());

        await _fixture.Network.ExecuteOnLoopAsync(() => _thiefSession.Set(SessionKeys.AccountType, AccountType.Regular));
        Assert.Equal(1, await CallAsync());
    }

    [Fact]
    public async Task ACriminalWithAGuardOnIt_GetsNoSecondOne_UntilTheGuardLeft()
    {
        Assert.Equal(1, await CallAsync());
        Assert.Equal(0, await CallAsync());

        _clock.Advance(TimeSpan.FromSeconds(40));
        await FireAsync();

        Assert.Equal(1, await CallAsync());
        Assert.Equal(2, _npcs.Spawns.Count);
    }

    [Fact]
    public async Task AfterItsTime_TheGuardLeaves_WithTheSameEffectAndSound()
    {
        await CallAsync();
        // The spawn put it in the world.
        _fixture.Mobiles.EnterWorld(_npcs.Spawned);

        _clock.Advance(TimeSpan.FromSeconds(39));
        await FireAsync();
        Assert.Empty(_npcs.Removals);

        _clock.Advance(TimeSpan.FromSeconds(1));
        await FireAsync();

        Assert.Equal([_npcs.Spawned.Id], _npcs.Removals);
        Assert.Equal(2, _effects.At.Count);
        Assert.Equal(2, _speech.PlacedSounds.Count);
    }

    [Fact]
    public async Task ASummonedGuardLeftByAStoppedServer_IsRemovedAtTheFirstCheck()
    {
        var leftover = new MobileEntity { Id = new Serial(0x300), Name = "a guard", TemplateId = "guard", Map = MapType.Trammel };
        leftover.SetProp("guard.summoned", true);
        var standing = new MobileEntity { Id = new Serial(0x301), Name = "a guard", TemplateId = "guard", Map = MapType.Trammel };
        _fixture.Mobiles.EnterWorld(leftover);
        _fixture.Mobiles.EnterWorld(standing);

        await FireAsync();
        await FireAsync();

        // The guard that stands in town by its spawn stays.
        Assert.Equal([leftover.Id], _npcs.Removals);
    }

    [Fact]
    public async Task ASpawnThatFails_FreesTheCriminalForTheNextCall()
    {
        _npcs.SpawnFailure = new KeyNotFoundException("no template");

        Assert.Equal(1, await CallAsync());
        Assert.Empty(_speech.Said);

        _npcs.SpawnFailure = null;
        Assert.Equal(1, await CallAsync());
        Assert.Single(_speech.Said);
    }

    private void Move(MobileEntity mobile, int x, int y)
    {
        Assert.True(_fixture.Mobiles.MoveTo(mobile, MapType.Trammel, new Point3D(x, y, 0)));
    }

    [Fact]
    public async Task ACriminalOutsideTheGuardedRegion_IsOutOfTheGuardsReach()
    {
        // The caller stands in town, the criminal ten tiles away, past the town's edge.
        Move(_aria, 95, 50);
        Move(_thief, 105, 50);

        Assert.Equal(0, await CallAsync());

        Move(_thief, 99, 50);
        Assert.Equal(1, await CallAsync());
    }

    [Fact]
    public async Task AGuardThatWasCalled_IsNeverItselfATarget()
    {
        var guard = new MobileEntity { Id = new Serial(0x400), Name = "a guard", TemplateId = "guard", Map = MapType.Trammel, Criminal = true };
        guard.SetProp("guard.summoned", true);
        _fixture.Mobiles.EnterWorld(guard);
        Move(guard, 55, 50);
        _thief.Criminal = false;

        Assert.Equal(0, await CallAsync());
    }

    [Fact]
    public async Task AMobileWhoseSummonedPropIsNotABool_DoesNotStopTheGuardsFromLeaving()
    {
        var odd = new MobileEntity { Id = new Serial(0x401), Name = "an orc", TemplateId = "orc", Map = MapType.Trammel };
        odd.SetProp("guard.summoned", "yes please");
        _fixture.Mobiles.EnterWorld(odd);
        await CallAsync();
        _fixture.Mobiles.EnterWorld(_npcs.Spawned);

        _clock.Advance(TimeSpan.FromSeconds(40));
        await FireAsync();

        Assert.Equal([_npcs.Spawned.Id], _npcs.Removals);
    }

    // On the game loop, as the speech handler and the timer wheel call the service; then what it started elsewhere
    // is awaited, and what that posted back to the loop is let run.
    private async Task<int> CallAsync()
    {
        var sent = 0;
        await _fixture.Network.ExecuteOnLoopAsync(() => sent = _guards.Call(_aria));
        await SettleAsync();

        return sent;
    }

    private async Task HeardAsync(string text, IReadOnlyList<int>? keywords = null)
    {
        await _fixture.Network.ExecuteOnLoopAsync(() => _guards.Heard(_aria, text, keywords));
        await SettleAsync();
    }

    private async Task FireAsync()
    {
        await _fixture.Network.ExecuteOnLoopAsync(() => _timers.Fire(_timers.Timers[0].Id));
        await SettleAsync();
    }

    private async Task SettleAsync()
    {
        await _guards.Running.WaitAsync(TimeSpan.FromSeconds(10));
        await _fixture.Network.ExecuteOnLoopAsync(() => { });
    }
}
