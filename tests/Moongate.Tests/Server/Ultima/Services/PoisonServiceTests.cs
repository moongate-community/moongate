using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.Combat;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Randomness;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Death;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class PoisonServiceTests : IAsyncLifetime
{
    private const long Aria = 2;
    private const long Boris = 3;

    private readonly RecordingTimerService _timers = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly StubDeathService _death = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly ScriptedRandom _random = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _boris = null!;
    private PoisonService _poison = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync((int)Aria);
        await _fixture.AddAsync((int)Boris);
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Aria), out _aria!));
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Boris), out _boris!));
        _aria.AccountId = new Serial(0x42);
        _boris.AccountId = new Serial(0x43);
        (_aria.HitsMax, _aria.Hits) = (100, 100);
        _poison = new(
            _state,
            _death,
            _timers,
            _fixture.Sessions,
            _fixture.Sender,
            _fixture.Sectors,
            _speech,
            new CombatConfig { DisplayDamageNumbers = true },
            _random,
            _fixture.Mobiles
        );
    }

    [Fact]
    public void Apply_PoisonsTheMobile_SavesTheLevel_TurnsTheBarGreen_AndTellsThoseAround()
    {
        Assert.Equal(PoisonResultType.Poisoned, _poison.Apply(_aria, 2));

        Assert.Equal(2, _poison.LevelOf(_aria));
        Assert.Equal(2L, _aria.GetProp<long>(PoisonService.LevelProp));
        var timer = Assert.Single(_timers.Timers);
        Assert.Equal((TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3.5), true), (timer.Interval, timer.Delay, timer.Repeat));
        Assert.Contains(
            _fixture.Sender.Sent.OfType<HealthBarStatusPacket>(),
            bar => (bar.Serial, bar.Kind, bar.Level) == ((uint)Aria, HealthBarType.Poison, 3)
        );
        Assert.Contains(_speech.SaidTo, said => said.Player == _aria && said.Cliloc == 1042861);
        Assert.Contains(_speech.SaidTo, said => said.Player == _boris && said.Cliloc == 1042862);
    }

    [Fact]
    public void AWeakerPoison_DoesNotReplaceAStrongerOne_AStrongerDoes()
    {
        _poison.Apply(_aria, 2);

        Assert.Equal(PoisonResultType.HigherActive, _poison.Apply(_aria, 1));
        Assert.Equal(2, _poison.LevelOf(_aria));

        Assert.Equal(PoisonResultType.Poisoned, _poison.Apply(_aria, 3));
        Assert.Equal(3, _poison.LevelOf(_aria));
        Assert.Single(_timers.Unregistered);
    }

    [Fact]
    public void ATick_TakesAShareOfTheHits_BetweenTheLevelsBounds()
    {
        _poison.Apply(_aria, 3);
        // Deadly: 1 + 12.5% of 100 hits.
        _random.Integers(1);

        Fire();

        Assert.Equal(87, _aria.Hits);
        Assert.Contains(_fixture.Sender.Sent.OfType<DamagePacket>(), damage => damage.Damage == 13);
    }

    [Fact]
    public void ThePoison_WearsOffAfterItsTicks()
    {
        _poison.Apply(_aria, 0);

        for (var tick = 0; tick < 11; tick++)
        {
            Fire();
        }

        Assert.Null(_poison.LevelOf(_aria));
        Assert.False(_aria.TryGetProp<long>(PoisonService.LevelProp, out _));
        Assert.Contains(_speech.ToldClilocs, told => told.Player == _aria && told.Cliloc == 502136);
        Assert.Single(_timers.Unregistered);
    }

    [Fact]
    public void APoisonThatKills_EndsWithTheDeath()
    {
        _aria.Hits = 5;
        _poison.Apply(_aria, 3);

        Fire();

        Assert.Equal(_aria, Assert.Single(_death.Killed).Mobile);
        Assert.Null(_poison.LevelOf(_aria));
    }

    [Fact]
    public void APlayerThatCannotDie_IsLeftWithOneHit()
    {
        _aria.Hits = 5;
        _death.Kills = false;
        _poison.Apply(_aria, 3);

        Fire();

        Assert.Equal(1, _aria.Hits);
        Assert.Null(_poison.LevelOf(_aria));
    }

    [Fact]
    public void Cure_EndsThePoison_AndTheGreenBar()
    {
        _poison.Apply(_aria, 1);

        Assert.True(_poison.Cure(_aria));
        Assert.False(_poison.Cure(_aria));

        Assert.Null(_poison.LevelOf(_aria));
        Assert.Contains(_fixture.Sender.Sent.OfType<HealthBarStatusPacket>(), bar => bar.Level == 0);
    }

    [Fact]
    public void Leaving_StopsTheTicks_ButKeepsThePoison_AndComingBackStartsThemOnce()
    {
        _poison.Apply(_aria, 1);

        _poison.OnSessionClosed(_session);
        Assert.Single(_timers.Unregistered);
        Assert.Equal(1, _poison.LevelOf(_aria));

        _poison.Resume(_aria);
        _poison.Resume(_aria);

        // The one it had, gone with the session, and one more: never two at once.
        Assert.Single(_timers.Timers);
        Assert.Single(_timers.Unregistered);
    }

    [Fact]
    public void ACorruptLevel_IsNoPoison()
    {
        _aria.SetProp(PoisonService.LevelProp, "green");

        Assert.Null(_poison.LevelOf(_aria));
        Assert.False(_poison.Cure(_aria));
        _poison.Resume(_aria);
        Assert.Empty(_timers.Timers);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void AnUnknownLevel_IsRefused(int level)
    {
        Assert.Equal(PoisonResultType.Refused, _poison.Apply(_aria, level));
    }

    [Fact]
    public void AMobileThatLeftTheWorld_TicksNoMore_AndNothingIsSentOfIt()
    {
        _poison.Apply(_aria, 1);
        _fixture.Sender.Sent.Clear();
        _fixture.Mobiles.LeaveWorld(_aria.Id);

        Fire();

        Assert.Empty(_fixture.Sender.Sent);
        Assert.Single(_timers.Unregistered);
    }

    [Fact]
    public void APoisonTakenAwayElsewhere_AsByDeath_StopsTheTicks()
    {
        _poison.Apply(_aria, 1);
        _aria.RemoveProp(PoisonService.LevelProp);

        Fire();

        Assert.Equal(100, _aria.Hits);
        Assert.Single(_timers.Unregistered);
    }

    [Fact]
    public void TheCount_IsSaved_SoComingBackDoesNotStartItAgain()
    {
        _poison.Apply(_aria, 0);

        for (var tick = 0; tick < 9; tick++)
        {
            Fire();
        }

        _poison.OnSessionClosed(_session);
        _poison.Resume(_aria);
        Fire();
        Fire();

        Assert.Null(_poison.LevelOf(_aria));
    }

    [Fact]
    public void AHiddenMobile_IsNotSeenToLookIll()
    {
        _aria.Hidden = true;

        _poison.Apply(_aria, 1);

        Assert.DoesNotContain(_speech.SaidTo, said => said.Player == _boris);
        Assert.Contains(_speech.SaidTo, said => said.Player == _aria);
    }

    [Fact]
    public void APoisonThatKills_NamesWhoPoisoned()
    {
        _aria.Hits = 5;
        _poison.Apply(_aria, 3, _boris);

        Fire();

        Assert.Equal((_aria, _boris), Assert.Single(_death.Killed));
    }

    [Fact]
    public void IsPoisoned_ReadsTheSavedLevel_AndACorruptOneIsNot()
    {
        Assert.False(PoisonService.IsPoisoned(_aria));
        _aria.SetProp(PoisonService.LevelProp, 2L);
        Assert.True(PoisonService.IsPoisoned(_aria));
        _aria.SetProp(PoisonService.LevelProp, "green");
        Assert.False(PoisonService.IsPoisoned(_aria));
    }

    private void Fire()
    {
        _timers.Fire(_timers.Timers.Last().Id);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
