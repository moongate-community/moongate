using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Ultima.Types;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class RegenerationServiceTests
{
    private readonly SettableClock _clock = new();
    private readonly RecordingMobileStateService _state = new();
    private readonly RegenerationConfig _config = new();
    private readonly RegenerationService _regeneration;

    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Trammel,
        Location = new Point3D(1496, 1628, 0), Hits = 100, HitsMax = 100, Mana = 100, ManaMax = 100, Stamina = 100,
        StaminaMax = 100, Hunger = 20
    };

    public RegenerationServiceTests()
    {
        _regeneration = new(_state, _config, _clock);
        // The recording service applies what it is asked, as the real one.
        _state.Apply = true;
    }

    [Fact]
    public void Tick_AFullMobile_ChangesNothing()
    {
        Pass(60);

        Assert.Empty(_state.Stats);
    }

    [Fact]
    public void Tick_AGhost_GetsNothingBack()
    {
        _aria.Body = 0x0192;
        _aria.Hits = 0;
        _aria.Stamina = 0;

        _regeneration.Tick(_aria);
        Pass(60);
        _regeneration.Tick(_aria);

        Assert.Empty(_state.Stats);
    }

    [Fact]
    public void Tick_HitsBelowTheMaximum_ComeBackOneEveryElevenSeconds()
    {
        _aria.Hits = 50;

        // The first tick starts the wait.
        _regeneration.Tick(_aria);
        Pass(10);
        Assert.Empty(_state.Stats);

        Pass(1);
        Assert.Equal(51, _aria.Hits);
        Assert.Equal(51, Assert.Single(_state.Stats).Change.Hits);

        Pass(22);
        Assert.Equal(53, _aria.Hits);
    }

    [Fact]
    public void Tick_StaminaComesBackEverySevenSeconds_AndStopsAtTheMaximum()
    {
        _aria.Stamina = 98;
        _regeneration.Tick(_aria);

        Pass(60);

        Assert.Equal(100, _aria.Stamina);
        Assert.Equal(2, _state.Stats.Count);
    }

    [Theory,
     // Nothing to think with: seven seconds.
     InlineData(0, 0, 7.0),
     // ModernUO's classic curve on half of intelligence plus meditation.
     InlineData(100, 0, 3.01),
     InlineData(100, 1000, 1.0),
     InlineData(125, 1000, 1.0),
     InlineData(140, 1000, 0.75)]
    public void ManaSeconds_FollowIntelligenceAndMeditation(int intelligence, int meditation, double seconds)
    {
        _aria.Intelligence = intelligence;
        _aria.Skills = [new MobileSkill { Skill = SkillType.Meditation, Base = meditation }];

        Assert.Equal(seconds, _regeneration.ManaSeconds(_aria), 2);
    }

    [Fact]
    public void Tick_TicksThatComeALittleEarly_DoNotSlowTheRateDown()
    {
        // A point a second (intelligence 100, Meditation 100), ticked every 990 ms as a timer wheel may.
        _aria.Intelligence = 100;
        _aria.Skills = [new MobileSkill { Skill = SkillType.Meditation, Base = 1000 }];
        _aria.Mana = 0;
        _regeneration.Tick(_aria);

        for (var tick = 0; tick < 100; tick++)
        {
            _clock.Advance(TimeSpan.FromMilliseconds(990));
            _regeneration.Tick(_aria);
        }

        // 99 seconds: 98 or 99 points, not the 50 a late point every other tick would give.
        Assert.InRange(_aria.Mana, 98, 99);
    }

    [Fact]
    public void Tick_ARateFasterThanTheTick_GivesThePointsOfTheWholeTick()
    {
        _aria.Mana = 0;
        _aria.SetProp("regen.mana", 0.5);
        _regeneration.Tick(_aria);

        Pass(10);

        Assert.Equal(20, _aria.Mana);
    }

    [Fact]
    public void Tick_AfterALongSleep_GivesAFewPointsAtOnce_NotAllOfThem()
    {
        _aria.Hits = 10;
        _regeneration.Tick(_aria);

        _clock.Advance(TimeSpan.FromHours(1));
        _regeneration.Tick(_aria);

        Assert.Equal(15, _aria.Hits);
    }

    [Theory, InlineData(1e16), InlineData(-5.0), InlineData(0.0)]
    public void Tick_APropOutOfRange_IsKeptWithinTheRangeOfTheConfiguration_OrIgnored(double seconds)
    {
        _aria.Hits = 50;
        _aria.SetProp("regen.hits", seconds);
        _regeneration.Tick(_aria);

        Pass(11);

        // An hour at most for the huge one; the configured eleven seconds for what is not a rate.
        Assert.Equal(seconds > 1 ? 50 : 51, _aria.Hits);
    }

    [Fact]
    public void ManaSeconds_WithAnotherSlowestRate_KeepTheShapeOfTheCurve()
    {
        _config.ManaSeconds = 14;
        _aria.Skills = [new MobileSkill { Skill = SkillType.Meditation, Base = 1000 }];

        _aria.Intelligence = 0;
        var half = _regeneration.ManaSeconds(_aria);
        _aria.Intelligence = 100;
        var full = _regeneration.ManaSeconds(_aria);
        _aria.Intelligence = 140;
        var more = _regeneration.ManaSeconds(_aria);
        _aria.Skills = [];
        _aria.Intelligence = 0;

        // Twice ModernUO's seven seconds: twice its curve, and more points are never slower.
        Assert.Equal(14.0, _regeneration.ManaSeconds(_aria), 2);
        Assert.Equal((6.02, 2.0, 1.5), (Math.Round(half, 2), Math.Round(full, 2), Math.Round(more, 2)));
    }

    [Fact]
    public void Tick_AStatThatBecomesFullAndDropsAgain_WaitsAWholeIntervalAgain()
    {
        _aria.Hits = 99;
        _regeneration.Tick(_aria);
        Pass(11);
        Assert.Equal(100, _aria.Hits);

        Pass(30);
        _aria.Hits = 90;
        _regeneration.Tick(_aria);
        Pass(10);

        Assert.Equal(90, _aria.Hits);
    }

    [Fact]
    public void Tick_APropOfTheMobile_ReplacesTheConfiguredSeconds()
    {
        _aria.Hits = 50;
        _aria.SetProp("regen.hits", 2L);
        _regeneration.Tick(_aria);

        Pass(4);

        Assert.Equal(52, _aria.Hits);
    }

    [Fact]
    public void Tick_AStarvingPlayer_GetsNoHitsBack_ButStaminaAndMana_AndAnNpcIsNotHungry()
    {
        var orc = new MobileEntity
        {
            Id = new Serial(0x100), Name = "an orc", TemplateId = "orc", Hits = 10, HitsMax = 50, Mana = 0, ManaMax = 0,
            Stamina = 50, StaminaMax = 50
        };
        _aria.Hunger = 0;
        _aria.Hits = 50;
        _aria.Stamina = 50;
        _regeneration.Tick(_aria);
        _regeneration.Tick(orc);

        _clock.Advance(TimeSpan.FromSeconds(11));
        _regeneration.Tick(_aria);
        _regeneration.Tick(orc);

        Assert.Equal((50, 51, 11), (_aria.Hits, _aria.Stamina, orc.Hits));

        // With hunger turned off in the configuration, an empty stomach changes nothing.
        _config.HungerEnabled = false;
        _regeneration.Tick(_aria);
        _clock.Advance(TimeSpan.FromSeconds(11));
        _regeneration.Tick(_aria);
        Assert.Equal(51, _aria.Hits);
    }

    [Fact]
    public void Tick_APoisonedMobile_GetsNoHitsBack_ButStamina()
    {
        _aria.SetProp(PoisonService.LevelProp, 1L);
        _aria.Hits = 50;
        _aria.Stamina = 50;
        _regeneration.Tick(_aria);

        _clock.Advance(TimeSpan.FromSeconds(11));
        _regeneration.Tick(_aria);

        Assert.Equal((50, 51), (_aria.Hits, _aria.Stamina));
    }

    [Fact]
    public void Tick_AParchedPlayer_GetsNoStaminaBack_ButHits_AndAnNpcIsNotThirsty()
    {
        var orc = new MobileEntity
        {
            Id = new Serial(0x100), Name = "an orc", TemplateId = "orc", Hits = 50, HitsMax = 50, Mana = 0, ManaMax = 0,
            Stamina = 10, StaminaMax = 50, Thirst = 0
        };
        _aria.Thirst = 0;
        _aria.Hits = 50;
        _aria.Stamina = 50;
        _regeneration.Tick(_aria);
        _regeneration.Tick(orc);

        _clock.Advance(TimeSpan.FromSeconds(11));
        _regeneration.Tick(_aria);
        _regeneration.Tick(orc);

        Assert.Equal((51, 50, 11), (_aria.Hits, _aria.Stamina, orc.Stamina));

        // With thirst turned off in the configuration, a dry throat changes nothing.
        _config.ThirstEnabled = false;
        _regeneration.Tick(_aria);
        _clock.Advance(TimeSpan.FromSeconds(7));
        _regeneration.Tick(_aria);
        Assert.Equal(51, _aria.Stamina);
    }

    [Fact]
    public async Task Tick_AStarvingGameMaster_StillGetsHitsBack_ItsHungerNeverDrops()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var session = await fixture.AddAsync(2);
        await fixture.Network.ExecuteOnLoopAsync(() => session.Set(SessionKeys.AccountType, AccountType.GameMaster));
        Assert.True(fixture.Mobiles.TryGet(new Serial(2), out var staff));
        staff.AccountId = new Serial(0x42);
        staff.Hunger = 0;
        staff.Hits = 5;
        staff.HitsMax = 10;
        var service = new RegenerationService(_state, _config, _clock, null, fixture.Sessions, fixture.Mobiles);

        service.Tick(staff);
        _clock.Advance(TimeSpan.FromSeconds(11));
        service.Tick(staff);

        Assert.Equal(6, staff.Hits);
    }

    [Fact]
    public async Task StartAsync_RegistersOneRepeatingTimerEverySecond_ThatTicksThePlayersInTheWorld()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(2);
        Assert.True(fixture.Mobiles.TryGet(new Serial(2), out var player));
        player.Stamina = 1;
        player.StaminaMax = 10;
        var timers = new RecordingTimerService();
        var service = new RegenerationService(_state, _config, _clock, timers, fixture.Sessions, fixture.Mobiles);

        await service.StartAsync();
        var timer = Assert.Single(timers.Timers);
        Assert.Equal(("regeneration", TimeSpan.FromSeconds(1), true), (timer.Name, timer.Interval, timer.Repeat));

        timers.Fire(timer.Id);
        _clock.Advance(TimeSpan.FromSeconds(7));
        timers.Fire(timer.Id);
        Assert.Equal(2, player.Stamina);

        await service.StopAsync();
        Assert.Equal([timer.Id], timers.Unregistered);
    }

    // A tick every second, as the service gives the players.
    private void Pass(int seconds)
    {
        for (var second = 0; second < seconds; second++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            _regeneration.Tick(_aria);
        }
    }
}
