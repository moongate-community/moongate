using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Weather;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Weather;
using Moongate.Tests.TestSupport.Random;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Maps;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class WeatherServiceTests : IAsyncLifetime
{
    private static readonly RegionContent SnowyHills = new() { Map = MapType.Felucca, Name = "Hills", Weather = "snowy" };

    private readonly Container _container = new();
    private readonly RecordingTimerService _timers = new();
    private readonly FakeMapService _map = new(200, 200);
    private readonly WorldConfig _world = new();
    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(1), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Felucca, Location = new Point3D(50, 50, 0)
    };

    private BroadcastFixture _fixture = null!;
    private WeatherService _weather = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(1, map: MapType.Felucca);
        _container.RegisterMoongateEventBus();
        await StartAsync(new ScriptedRandom(1));
    }

    [Fact]
    public async Task Login_SendsTheMapsWeatherOutsideEveryRegion()
    {
        _weather.RegionChanged(_aria, null, null);

        await LoginAsync();

        Assert.Equal([(WeatherKindType.Rain, 1L)], Sent());
    }

    [Fact]
    public void BeforeTheLogin_NothingIsSent()
    {
        _weather.RegionChanged(_aria, null, SnowyHills);

        _timers.Fire(CheckTimer());

        Assert.Empty(Sent());
    }

    [Fact]
    public async Task ARegionChange_SendsTheNewRegionsWeatherAtOnce()
    {
        _weather.RegionChanged(_aria, null, null);
        await LoginAsync();

        _weather.RegionChanged(_aria, null, SnowyHills);

        Assert.Equal([(WeatherKindType.Rain, 1L), (WeatherKindType.Snow, 1L)], Sent());
    }

    [Fact]
    public async Task TheCheck_SendsOnlyWhatChanged_AndInsideABuildingItIsDry()
    {
        _weather.RegionChanged(_aria, null, null);
        await LoginAsync();
        _timers.Fire(CheckTimer());
        Assert.Single(Sent());

        _map.AddStatic(50, 50, 0x0600, 20);
        _timers.Fire(CheckTimer());

        Assert.Equal([(WeatherKindType.Rain, 1L), (WeatherKindType.None, 1L)], Sent());
    }

    [Fact]
    public async Task AStorm_ThundersSometimesForThePlayerOutside()
    {
        await _weather.StopAsync();
        await StartAsync(new ScriptedRandom(0), storms: true);
        _weather.RegionChanged(_aria, null, null);
        await LoginAsync();

        _timers.Fire(CheckTimer());

        var thunder = Assert.Single(_fixture.Sender.Sent.OfType<PlaySoundPacket>());
        Assert.Equal((0x28, _aria.Location), (thunder.Sound, thunder.Location));
    }

    [Fact]
    public async Task Force_ChangesTheProfilesWeatherUntilTheNextHour()
    {
        _weather.RegionChanged(_aria, null, null);
        await LoginAsync();

        _weather.Force("rainy", WeatherKindType.None);
        _timers.Fire(CheckTimer());
        Assert.Equal(WeatherKindType.None, _weather.StateOf("rainy").Kind);

        var hour = _timers.Timers.Single(timer => timer.Name == WeatherService.HourTimerName);
        Assert.Equal(TimeSpan.FromMinutes(5), hour.Interval);
        _timers.Fire(hour.Id);

        Assert.Equal(WeatherKindType.Rain, _weather.StateOf("rainy").Kind);
        Assert.Equal("rainy", _weather.ProfileOf(_aria));
    }

    [Fact]
    public async Task Left_StopsSendingToThePlayer()
    {
        _weather.RegionChanged(_aria, null, null);
        await LoginAsync();

        _weather.Left(_aria.Id);
        _weather.Force("rainy", WeatherKindType.Snow);
        _timers.Fire(CheckTimer());

        Assert.Single(Sent());
    }

    public async Task DisposeAsync()
    {
        await _weather.StopAsync();
        _container.Dispose();
        await _fixture.DisposeAsync();
    }

    private async Task StartAsync(System.Random random, bool storms = false)
    {
        var map = new MapContent { Map = MapType.Felucca, Name = "Felucca", Weather = "rainy" };
        var data = storms
            ? new StubDataLoaderService().With(new WeatherContent { Name = "rainy", StormChance = 100 }).With(map)
            : new StubDataLoaderService()
              .With(
                  new WeatherContent { Name = "none" },
                  new WeatherContent { Name = "rainy", RainChance = 100, MinTemperature = 10, MaxTemperature = 10 },
                  new WeatherContent { Name = "snowy", SnowChance = 100, SnowThreshold = 50 }
              )
              .With(map);

        _timers.Timers.Clear();
        _weather = new(data, _map, _fixture.Sessions, _fixture.Sender, _timers, _container.Resolve<IMoongateEventBus>(), _world, random);
        await _weather.StartAsync();
    }

    private async Task LoginAsync()
    {
        await _container.Resolve<IMoongateEventBus>().PublishAsync(new CharacterEnteredWorldEvent(_aria));
    }

    private string CheckTimer()
    {
        return _timers.Timers.Single(timer => timer.Name == WeatherService.CheckTimerName).Id;
    }

    private List<(WeatherKindType, long)> Sent()
    {
        return _fixture.Sender.Sent
                       .Select((packet, index) => (packet, index))
                       .Where(pair => pair.packet is WeatherPacket)
                       .Select(pair => (((WeatherPacket)pair.packet).Kind, _fixture.Sender.SentSessionIds[pair.index]))
                       .ToList();
    }
}
