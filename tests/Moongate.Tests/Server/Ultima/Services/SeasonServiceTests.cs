using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class SeasonServiceTests : IAsyncLifetime
{
    private static readonly RegionContent IceCave = new()
        { Map = MapType.Trammel, Name = "Ice", Season = SeasonType.Winter };

    private static readonly RegionContent Town = new() { Map = MapType.Trammel, Name = "Town" };

    private readonly Container _container = new();
    private readonly RecordingTimerService _timers = new();
    private readonly StubClockService _clock = new();
    private readonly WorldConfig _world = new();
    private readonly RecordingLightService _light = new();
    private readonly StubWeatherService _weather = new();

    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(1), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Trammel,
        Location = new Point3D(50, 50, 0)
    };

    private BroadcastFixture _fixture = null!;
    private SeasonService _seasons = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(1);
        _container.RegisterMoongateEventBus();
        var data = new StubDataLoaderService().With(
            new MapContent { Map = MapType.Felucca, Name = "Felucca", Season = SeasonType.Desolation },
            new MapContent { Map = MapType.Trammel, Name = "Trammel", Season = SeasonType.Spring }
        );
        _seasons = new(
            data,
            _clock,
            _world,
            _fixture.Sessions,
            _fixture.Sender,
            _timers,
            _container.Resolve<IMoongateEventBus>(),
            _fixture.Network.Loop,
            _light,
            _weather
        );
        await _seasons.StartAsync();
    }

    [Fact]
    public void SeasonOf_IsTheRegionsSeason_ElseTheMaps()
    {
        _seasons.RegionChanged(_aria, null, Town);
        Assert.Equal(SeasonType.Spring, _seasons.SeasonOf(_aria));

        _seasons.RegionChanged(_aria, Town, IceCave);
        Assert.Equal(SeasonType.Winter, _seasons.SeasonOf(_aria));
    }

    [Fact]
    public async Task Login_AfterTheSeasonTheLoginSent_SendsNothingMore()
    {
        _seasons.RegionChanged(_aria, null, null);

        Assert.Equal(SeasonType.Spring, _seasons.SeasonOnLogin(_aria));
        await LoginAsync();

        Assert.Empty(Sent());
    }

    [Fact]
    public void BeforeTheLogin_NothingIsSent()
    {
        _seasons.RegionChanged(_aria, null, IceCave);

        Assert.Empty(Sent());
    }

    [Fact]
    public async Task ARegionWithAnotherSeason_SendsIt_ThenTheLightAndTheWeatherAgain()
    {
        await EnterAsync(null);

        _seasons.RegionChanged(_aria, null, Town);
        _seasons.RegionChanged(_aria, Town, IceCave);

        var packets = _fixture.Sender.Sent.Where(packet => packet is SeasonChangePacket or GlobalLightLevelPacket).ToList();
        Assert.Equal(
            (SeasonType.Winter, true),
            (((SeasonChangePacket)packets[0]).Season, ((SeasonChangePacket)packets[0]).PlaySound)
        );
        Assert.IsType<GlobalLightLevelPacket>(packets[1]);
        Assert.Equal(2, packets.Count);
        Assert.Equal([_aria], _weather.Resent);
    }

    [Theory, InlineData(0, SeasonType.Spring), InlineData(12, SeasonType.Summer), InlineData(35, SeasonType.Fall),
     InlineData(36, SeasonType.Winter), InlineData(48, SeasonType.Spring)]
    public void Rotation_MovesTheMapsSeasonEveryDaysPerSeason(long day, SeasonType expected)
    {
        _world.SeasonRotation = true;
        _clock.Day = day;

        Assert.Equal(expected, _seasons.SeasonOf(MapType.Trammel));
        Assert.Equal(SeasonType.Desolation, _seasons.SeasonOf(MapType.Felucca));
    }

    [Fact]
    public void Rotation_BeforeTheWorldStart_StillGivesASeason()
    {
        _world.SeasonRotation = true;
        _clock.Day = -1;

        Assert.Equal(SeasonType.Winter, _seasons.SeasonOf(MapType.Trammel));
    }

    [Fact]
    public void WithoutRotation_TheMapKeepsItsSeason()
    {
        _clock.Day = 100;

        Assert.Equal(SeasonType.Spring, _seasons.SeasonOf(MapType.Trammel));
    }

    [Fact]
    public async Task TheCheck_SendsTheRotatedSeason_ButNotInsideARegionWithItsOwn()
    {
        _world.SeasonRotation = true;
        await EnterAsync(null);
        var bob = new MobileEntity
            { Id = new Serial(2), Name = "Bob", Map = MapType.Trammel, Location = new Point3D(60, 60, 0) };
        await _fixture.AddAsync(2);
        _seasons.RegionChanged(bob, null, IceCave);
        _seasons.SeasonOnLogin(bob);
        await LoginAsync(bob);

        _clock.Day = 12;
        _timers.Fire(CheckTimer());

        Assert.Equal([(SeasonType.Summer, 1L)], Sent());
    }

    [Fact]
    public async Task SetOverride_SendsTheMapsPlayersTheNewSeason_AndNullGoesBack()
    {
        await EnterAsync(null);

        _seasons.SetOverride(MapType.Trammel, SeasonType.Winter);
        Assert.Equal(SeasonType.Winter, _seasons.SeasonOf(MapType.Trammel));
        _seasons.SetOverride(MapType.Trammel, null);

        Assert.Equal([(SeasonType.Winter, 1L), (SeasonType.Spring, 1L)], Sent());
        Assert.Equal(SeasonType.Spring, _seasons.SeasonOf(MapType.Trammel));
    }

    [Fact]
    public async Task ARelogin_FollowsTheNewCharacter_AndForgetsTheOldOne()
    {
        await EnterAsync(null);
        var again = new MobileEntity { Id = _aria.Id, Name = "Aria", Map = MapType.Trammel, Location = _aria.Location };

        _seasons.RegionChanged(again, null, IceCave);
        _seasons.SeasonOnLogin(again);
        await LoginAsync(again);

        Assert.Equal((SeasonType.Winter, SeasonType.Spring), (_seasons.SeasonOf(again), _seasons.SeasonOf(_aria)));
        Assert.Empty(Sent());
    }

    [Fact]
    public void AMapMissingFromMapsToml_IsSummer_AndRotatesFromThere()
    {
        Assert.Equal(SeasonType.Summer, _seasons.SeasonOf(MapType.Ilshenar));

        _world.SeasonRotation = true;
        _clock.Day = 12;

        Assert.Equal(SeasonType.Fall, _seasons.SeasonOf(MapType.Ilshenar));
    }

    [Fact]
    public async Task APlayerWhoLeft_IsNotFollowed()
    {
        await EnterAsync(null);

        _seasons.Left(_aria.Id);
        _seasons.RegionChanged(_aria, null, IceCave);

        Assert.Empty(Sent());
    }

    [Fact]
    public async Task Stop_UnregistersTheCheck()
    {
        var id = CheckTimer();

        await _seasons.StopAsync();

        Assert.Contains(id, _timers.Unregistered);
    }

    private async Task EnterAsync(RegionContent? region)
    {
        _seasons.RegionChanged(_aria, null, region);
        _seasons.SeasonOnLogin(_aria);
        await LoginAsync();
    }

    private async Task LoginAsync(MobileEntity? character = null)
    {
        await _container.Resolve<IMoongateEventBus>().PublishAsync(new CharacterEnteredWorldEvent(character ?? _aria));
    }

    private string CheckTimer()
    {
        return _timers.Timers.Single(timer => timer.Name == SeasonService.CheckTimerName).Id;
    }

    private List<(SeasonType, long)> Sent()
    {
        return _fixture.Sender.Sent
            .Select((packet, index) => (packet, index))
            .Where(pair => pair.packet is SeasonChangePacket)
            .Select(pair => (((SeasonChangePacket)pair.packet).Season, _fixture.Sender.SentSessionIds[pair.index]))
            .ToList();
    }

    public async Task DisposeAsync()
    {
        await _seasons.StopAsync();
        _container.Dispose();
        await _fixture.DisposeAsync();
    }
}
