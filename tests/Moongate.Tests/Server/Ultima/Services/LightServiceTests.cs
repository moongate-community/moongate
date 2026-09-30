using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.World;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class LightServiceTests : IAsyncLifetime
{
    private readonly StubClockService _clock = new();
    private readonly RecordingTimerService _timers = new();
    private readonly WorldConfig _world = new();
    private BroadcastFixture _fixture = null!;
    private LightService _light = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _light = new(_clock, _fixture.Sessions, _fixture.Mobiles, _fixture.Sender, _timers, _fixture.Network.Loop, _world);
        await _light.StartAsync();
    }

    [Theory,
     InlineData(0, 0, 12), InlineData(3, 59, 12), InlineData(4, 0, 12), InlineData(5, 0, 6), InlineData(6, 0, 0),
     InlineData(12, 0, 0), InlineData(21, 59, 0), InlineData(22, 0, 0), InlineData(23, 0, 6), InlineData(23, 59, 11)]
    public void LevelFor_FollowsModernUOsBands(int hours, int minutes, int level)
    {
        _clock.Time = new GameTime(hours, minutes);

        Assert.Equal(level, _light.LevelFor(Mobile()));
    }

    [Fact]
    public void LevelFor_UsesTheConfiguredLevels()
    {
        _world.DayLight = 2;
        _world.NightLight = 20;

        _clock.Time = new GameTime(1, 0);
        Assert.Equal(20, _light.LevelFor(Mobile()));
        _clock.Time = new GameTime(5, 0);
        Assert.Equal(11, _light.LevelFor(Mobile()));
    }

    [Fact]
    public async Task Tick_SendsTheLevelToThePlayersInTheWorld_OnlyWhenItChanges()
    {
        await _fixture.AddAsync(1);
        await _fixture.AddAsync(2, entered: false);
        var timer = Assert.Single(_timers.Timers);
        Assert.Equal((LightService.TimerName, TimeSpan.FromSeconds(5), true), (timer.Name, timer.Interval, timer.Repeat));

        _timers.Fire(timer.Id);
        _timers.Fire(timer.Id);

        Assert.Equal([(1L, 0)], Sent());

        _clock.Time = new GameTime(1, 0);
        _timers.Fire(timer.Id);

        Assert.Equal([(1L, 0), (1L, 12)], Sent());
    }

    [Fact]
    public async Task LevelOnLogin_CountsAsSent()
    {
        await _fixture.AddAsync(1);
        _fixture.Mobiles.TryGet(new Serial(1), out var character);

        Assert.Equal(0, _light.LevelOnLogin(character!));
        _timers.Fire(Assert.Single(_timers.Timers).Id);

        Assert.Empty(Sent());
    }

    [Fact]
    public async Task SetOverrideAsync_SendsTheLevelAtOnce_AndClearingItGoesBackToTheClock()
    {
        await _fixture.AddAsync(1);
        _timers.Fire(Assert.Single(_timers.Timers).Id);

        await _light.SetOverrideAsync(25);
        Assert.Equal((25, 25), (_light.Override, _light.LevelFor(Mobile())));
        await _light.SetOverrideAsync(null);

        Assert.Null(_light.Override);
        Assert.Equal([(1L, 0), (1L, 25), (1L, 0)], Sent());
    }

    [Fact]
    public async Task StopAsync_RemovesTheTimer()
    {
        await _light.StopAsync();

        Assert.Empty(_timers.Timers);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    private List<(long, int)> Sent()
    {
        return _fixture.Sender.Sent
                       .Select((packet, index) => (packet, index))
                       .Where(pair => pair.packet is GlobalLightLevelPacket)
                       .Select(pair => (_fixture.Sender.SentSessionIds[pair.index], ((GlobalLightLevelPacket)pair.packet).Level))
                       .ToList();
    }

    private static MobileEntity Mobile()
    {
        return new() { Id = new Serial(9), Map = MapType.Trammel };
    }
}
