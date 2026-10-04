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
            new StubGameLoop(),
            _config,
            _clock
        );
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
    public void TheGuardsKeyword_InAGuardedRegion_BringsAGuardOntoTheCriminal_WithItsEffectSoundAndLine()
    {
        _guards.Heard(_aria, "qualcosa", GuardsKeyword);

        Assert.Equal(("guard", MapType.Trammel, new Point3D(60, 50, 0)), Assert.Single(_npcs.Spawns));
        Assert.True(_npcs.Spawned.GetProp("guard.summoned", false));
        var effect = Assert.Single(_effects.At);
        Assert.Equal((MapType.Trammel, new Point3D(60, 50, 0), GuardService.TeleportEffect), (effect.Map, effect.Location, effect.Options.Graphic));
        Assert.Equal((MapType.Trammel, new Point3D(60, 50, 0), GuardService.TeleportSound), Assert.Single(_speech.PlacedSounds));
        Assert.Equal((_npcs.Spawned, "Thou wilt regret thine actions, swine!"), Assert.Single(_speech.Said));
    }

    [Theory,
     InlineData("guards", true),
     InlineData("Guards! Help!", true),
     InlineData("GUARDS", true),
     InlineData("I love the vanguards of old", false),
     InlineData("hello", false)]
    public void ThePlainWord_CallsThemToo_ForAClientThatSendsNoKeyword(string text, bool called)
    {
        _guards.Heard(_aria, text);

        Assert.Equal(called ? 1 : 0, _npcs.Spawns.Count);
    }

    [Fact]
    public void OutsideAGuardedRegion_OrWithGuardsOff_NobodyComes()
    {
        Move(_aria, 500, 500);
        Move(_thief, 505, 500);
        Assert.Equal(0, _guards.Call(_aria));

        Move(_aria, 50, 50);
        Move(_thief, 60, 50);
        _config.GuardsEnabled = false;
        _guards.Heard(_aria, "guards", GuardsKeyword);

        Assert.Empty(_npcs.Spawns);
    }

    [Fact]
    public async Task OnlyACriminalInRange_ThatIsNotStaff_GetsAGuard()
    {
        _thief.Criminal = false;
        Assert.Equal(0, _guards.Call(_aria));

        _thief.Criminal = true;
        Move(_thief, 65, 50);
        Assert.Equal(0, _guards.Call(_aria));

        Move(_thief, 64, 50);
        await _fixture.Network.ExecuteOnLoopAsync(() => _thiefSession.Set(SessionKeys.AccountType, AccountType.GameMaster));
        Assert.Equal(0, _guards.Call(_aria));

        await _fixture.Network.ExecuteOnLoopAsync(() => _thiefSession.Set(SessionKeys.AccountType, AccountType.Regular));
        Assert.Equal(1, _guards.Call(_aria));
    }

    [Fact]
    public void ACriminalWithAGuardOnIt_GetsNoSecondOne_UntilTheGuardLeft()
    {
        Assert.Equal(1, _guards.Call(_aria));
        Assert.Equal(0, _guards.Call(_aria));

        _clock.Advance(TimeSpan.FromSeconds(40));
        Fire();

        Assert.Equal(1, _guards.Call(_aria));
        Assert.Equal(2, _npcs.Spawns.Count);
    }

    [Fact]
    public void AfterItsTime_TheGuardLeaves_WithTheSameEffectAndSound()
    {
        _guards.Call(_aria);
        // The spawn put it in the world.
        _fixture.Mobiles.EnterWorld(_npcs.Spawned);

        _clock.Advance(TimeSpan.FromSeconds(39));
        Fire();
        Assert.Empty(_npcs.Removals);

        _clock.Advance(TimeSpan.FromSeconds(1));
        Fire();

        Assert.Equal([_npcs.Spawned.Id], _npcs.Removals);
        Assert.Equal(2, _effects.At.Count);
        Assert.Equal(2, _speech.PlacedSounds.Count);
    }

    [Fact]
    public void ASummonedGuardLeftByAStoppedServer_IsRemovedAtTheFirstCheck()
    {
        var leftover = new MobileEntity { Id = new Serial(0x300), Name = "a guard", TemplateId = "guard", Map = MapType.Trammel };
        leftover.SetProp("guard.summoned", true);
        var standing = new MobileEntity { Id = new Serial(0x301), Name = "a guard", TemplateId = "guard", Map = MapType.Trammel };
        _fixture.Mobiles.EnterWorld(leftover);
        _fixture.Mobiles.EnterWorld(standing);

        Fire();
        Fire();

        // The guard that stands in town by its spawn stays.
        Assert.Equal([leftover.Id], _npcs.Removals);
    }

    [Fact]
    public void ASpawnThatFails_FreesTheCriminalForTheNextCall()
    {
        _npcs.SpawnFailure = new KeyNotFoundException("no template");

        Assert.Equal(1, _guards.Call(_aria));
        Assert.Empty(_speech.Said);

        _npcs.SpawnFailure = null;
        Assert.Equal(1, _guards.Call(_aria));
        Assert.Single(_speech.Said);
    }

    private void Move(MobileEntity mobile, int x, int y)
    {
        Assert.True(_fixture.Mobiles.MoveTo(mobile, MapType.Trammel, new Point3D(x, y, 0)));
    }

    private void Fire()
    {
        _timers.Fire(_timers.Timers[0].Id);
    }
}
