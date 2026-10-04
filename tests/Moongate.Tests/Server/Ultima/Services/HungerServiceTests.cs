using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class HungerServiceTests : IAsyncLifetime
{
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RegenerationConfig _config = new();

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
        _hunger = new(_timers, _fixture.Sessions, _fixture.Mobiles, _speech, _config);
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
        Assert.Equal(("hunger", TimeSpan.FromMinutes(5), true), (timer.Name, timer.Interval, timer.Repeat));

        await _hunger.StopAsync();
        Assert.Equal([timer.Id], _timers.Unregistered);
    }

    [Fact]
    public async Task StartAsync_WithHungerOff_RegistersNothing()
    {
        _config.HungerEnabled = false;

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

    [Theory, InlineData(25, 20), InlineData(-3, 0), InlineData(12, 12)]
    public void Set_KeepsTheHungerFromZeroToTwenty(int asked, int kept)
    {
        _hunger.Set(_aria, asked);

        Assert.Equal(kept, _aria.Hunger);
    }

    private void Decay(int times)
    {
        for (var time = 0; time < times; time++)
        {
            _timers.Fire(_timers.Timers[0].Id);
        }
    }
}
