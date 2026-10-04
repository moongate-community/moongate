using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class HungerServiceTests : IAsyncLifetime
{
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RegenerationConfig _config = new();
    private readonly SettableClock _clock = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _staffSession = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _staff = null!;
    private HungerService _hunger = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        _staffSession = await _fixture.AddAsync(3);
        await _fixture.Network.ExecuteOnLoopAsync(() => _staffSession.Set(SessionKeys.AccountType, AccountType.GameMaster));
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        Assert.True(_fixture.Mobiles.TryGet(new Serial(3), out _staff!));
        _hunger = new(_timers, _fixture.Sessions, _fixture.Mobiles, _speech, _config, _clock);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_RegistersOneRepeatingTimer_EveryHungerMinutes_AndStopAsyncRemovesIt()
    {
        await _hunger.StartAsync();

        var timer = Assert.Single(_timers.Timers);
        // A check every minute: each player has its own five minutes.
        Assert.Equal(("hunger", TimeSpan.FromMinutes(1), true), (timer.Name, timer.Interval, timer.Repeat));

        await _hunger.StopAsync();
        Assert.Equal([timer.Id], _timers.Unregistered);
    }

    [Fact]
    public async Task StartAsync_WithHungerAndThirstOff_RegistersNothing()
    {
        _config.HungerEnabled = false;
        _config.ThirstEnabled = false;

        await _hunger.StartAsync();

        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public async Task EveryInterval_APlayerInTheWorldLosesAPoint_TheStaffDoesNot()
    {
        await _hunger.StartAsync();

        Decay(3);

        Assert.Equal((17, 20), (_aria.Hunger, _staff.Hunger));
        Assert.Empty(_speech.Told);
    }

    [Fact]
    public async Task APlayer_IsToldWhenItGetsHungry_AndWhenItStarves_AndGoesNoLower()
    {
        await _hunger.StartAsync();
        _aria.Hunger = 6;

        Decay(1);
        Assert.Equal((_aria, "You are hungry."), Assert.Single(_speech.Told));

        Decay(5);
        Assert.Equal(0, _aria.Hunger);
        Assert.Equal("You are starving: your wounds will not heal until you eat.", _speech.Told[^1].Text);

        Decay(3);
        Assert.Equal(0, _aria.Hunger);
        Assert.Equal(2, _speech.Told.Count);
    }

    [Fact]
    public async Task EveryInterval_APlayerAlsoGetsThirstier_TheStaffDoesNot()
    {
        await _hunger.StartAsync();

        Decay(3);

        Assert.Equal((17, 20), (_aria.Thirst, _staff.Thirst));
    }

    [Fact]
    public async Task APlayer_IsToldWhenItGetsThirsty_AndWhenItIsParched_AndGoesNoLower()
    {
        await _hunger.StartAsync();
        _aria.Thirst = 6;

        Decay(1);
        Assert.Equal((_aria, "You are thirsty."), Assert.Single(_speech.Told));

        Decay(5);
        Assert.Equal(0, _aria.Thirst);
        Assert.Equal("You are parched: your stamina will not come back until you drink.", _speech.Told[^1].Text);

        Decay(3);
        Assert.Equal(0, _aria.Thirst);
        Assert.Equal(2, _speech.Told.Count);
    }

    [Fact]
    public async Task WithThirstOff_OnlyHungerDrops_AndWithHungerOff_OnlyThirst()
    {
        _config.ThirstEnabled = false;
        await _hunger.StartAsync();
        Decay(1);
        Assert.Equal((19, 20), (_aria.Hunger, _aria.Thirst));
        await _hunger.StopAsync();

        _config.ThirstEnabled = true;
        _config.HungerEnabled = false;
        await _hunger.StartAsync();
        _timers.Fire(_timers.Timers[^1].Id);

        for (var minute = 0; minute < 5; minute++)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            _timers.Fire(_timers.Timers[^1].Id);
        }

        Assert.Equal((19, 19), (_aria.Hunger, _aria.Thirst));
    }

    [Theory, InlineData(25, 20), InlineData(-3, 0), InlineData(12, 12)]
    public void SetThirst_KeepsTheThirstFromZeroToTwenty(int asked, int kept)
    {
        _hunger.SetThirst(_aria, asked);

        Assert.Equal(kept, _aria.Thirst);
    }

    [Theory, InlineData(25, 20), InlineData(-3, 0), InlineData(12, 12)]
    public void Set_KeepsTheHungerFromZeroToTwenty(int asked, int kept)
    {
        _hunger.Set(_aria, asked);

        Assert.Equal(kept, _aria.Hunger);
    }

    [Fact]
    public async Task APlayerThatEntersLater_HasItsOwnInterval()
    {
        await _hunger.StartAsync();
        Decay(0);

        _clock.Advance(TimeSpan.FromMinutes(4));
        await _fixture.AddAsync(4);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(4), out var late));
        Fire();

        // A minute later the first player's five minutes are over; the late one has four to go.
        _clock.Advance(TimeSpan.FromMinutes(1));
        Fire();
        Assert.Equal((19, 20), (_aria.Hunger, late.Hunger));

        _clock.Advance(TimeSpan.FromMinutes(4));
        Fire();
        Assert.Equal((19, 19), (_aria.Hunger, late.Hunger));
    }

    // Whole intervals: the first check starts each player's wait, then a check every minute.
    private void Decay(int times)
    {
        Fire();

        for (var minute = 0; minute < times * _config.HungerMinutes; minute++)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            Fire();
        }
    }

    private void Fire()
    {
        _timers.Fire(_timers.Timers[0].Id);
    }
}
