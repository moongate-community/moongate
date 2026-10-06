using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class MusicServiceTests : IAsyncLifetime
{
    private static readonly RegionContent Britain = new()
        { Map = MapType.Felucca, Name = "Britain", Music = MusicType.Britain1 };

    private static readonly RegionContent BritainBank = new()
        { Map = MapType.Felucca, Name = "Bank", Music = MusicType.Britain1 };

    private static readonly RegionContent Quiet = new() { Map = MapType.Felucca, Name = "Quiet" };

    private readonly Container _container = new();

    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(1), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Felucca,
        Location = new Point3D(50, 50, 0)
    };

    private BroadcastFixture _fixture = null!;
    private MusicService _music = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(1, map: MapType.Felucca);
        _container.RegisterMoongateEventBus();
        var data = new StubDataLoaderService().With(
            new MapContent { Map = MapType.Felucca, Name = "Felucca", Music = MusicType.Create1 },
            new MapContent { Map = MapType.Trammel, Name = "Trammel" }
        );
        _music = new(
            data,
            _fixture.Sessions,
            _fixture.Sender,
            _container.Resolve<IMoongateEventBus>(),
            _fixture.Network.Loop
        );
        await _music.StartAsync();
    }

    [Fact]
    public async Task Login_PlaysTheMusicOfTheRegion()
    {
        _music.RegionChanged(_aria, null, Britain);

        await LoginAsync();

        Assert.Equal([MusicType.Britain1], Sent());
    }

    [Fact]
    public void BeforeTheLogin_NothingPlays()
    {
        _music.RegionChanged(_aria, null, Britain);

        Assert.Empty(Sent());
    }

    [Fact]
    public async Task ARegionWithOtherMusic_ChangesIt_TheSameMusicDoesNot()
    {
        _music.RegionChanged(_aria, null, Britain);
        await LoginAsync();

        _music.RegionChanged(_aria, Britain, BritainBank);
        _music.RegionChanged(_aria, BritainBank, null);

        Assert.Equal([MusicType.Britain1, MusicType.Create1], Sent());
    }

    [Fact]
    public async Task NoMusicInTheRegionOrTheMap_StopsIt()
    {
        _music.RegionChanged(_aria, null, Britain);
        await LoginAsync();

        _music.RegionChanged(_aria, Britain, Quiet);
        _aria.Map = MapType.Trammel;
        _music.RegionChanged(_aria, Quiet, null);

        Assert.Equal([MusicType.Britain1, MusicType.Create1, MusicType.NoMusic], Sent());
    }

    [Fact]
    public async Task Play_PlaysATrackToThePlayer_AndTheNextRegionChangePutsItsOwnBack()
    {
        _music.RegionChanged(_aria, null, Britain);
        await LoginAsync();

        _music.Play(_aria, MusicType.Tavern04);
        _music.RegionChanged(_aria, Britain, BritainBank);

        Assert.Equal([MusicType.Britain1, MusicType.Tavern04, MusicType.Britain1], Sent());
        Assert.Equal(MusicType.Britain1, _music.MusicOf(_aria));
    }

    [Fact]
    public async Task APlayerWhoLeft_IsNotFollowed()
    {
        _music.RegionChanged(_aria, null, Britain);
        await LoginAsync();

        _music.Left(_aria.Id);
        _music.RegionChanged(_aria, Britain, Quiet);

        Assert.Equal([MusicType.Britain1], Sent());
    }

    [Fact]
    public async Task Play_TheTrackAlreadyPlaying_SendsItAgain()
    {
        _music.RegionChanged(_aria, null, Britain);
        await LoginAsync();

        Assert.True(_music.Play(_aria, MusicType.Britain1));

        Assert.Equal([MusicType.Britain1, MusicType.Britain1], Sent());
    }

    [Fact]
    public void Play_ToAPlayerNotFollowed_SendsNothing_AndSaysSo()
    {
        Assert.False(_music.Play(_aria, MusicType.Tavern04));

        Assert.Empty(Sent());
    }

    [Fact]
    public async Task ALoginOutsideAnyRegion_PlaysTheMapMusic()
    {
        _music.RegionChanged(_aria, null, null);

        await LoginAsync();

        Assert.Equal([MusicType.Create1], Sent());
    }

    [Fact]
    public async Task ALoginAfterLeaving_PlaysNothing()
    {
        _music.RegionChanged(_aria, null, Britain);
        _music.Left(_aria.Id);

        await LoginAsync();

        Assert.Empty(Sent());
    }

    [Fact]
    public async Task ARelogin_FollowsTheNewCharacter_AndPlaysItsMusicAgain()
    {
        _music.RegionChanged(_aria, null, Britain);
        await LoginAsync();
        var again = new MobileEntity { Id = _aria.Id, Name = "Aria", Map = MapType.Felucca, Location = _aria.Location };

        _music.RegionChanged(again, null, Britain);
        await LoginAsync(again);
        _music.RegionChanged(_aria, Britain, Quiet);

        Assert.Equal([MusicType.Britain1, MusicType.Britain1], Sent());
    }

    [Fact]
    public void MusicOf_APlayerNotFollowed_IsItsMapMusic()
    {
        Assert.Equal(MusicType.Create1, _music.MusicOf(_aria));
    }

    public async Task DisposeAsync()
    {
        await _music.StopAsync();
        _container.Dispose();
        await _fixture.DisposeAsync();
    }

    private async Task LoginAsync(MobileEntity? character = null)
    {
        await _container.Resolve<IMoongateEventBus>().PublishAsync(new CharacterEnteredWorldEvent(character ?? _aria));
    }

    private List<MusicType> Sent()
    {
        return _fixture.Sender.Sent.OfType<PlayMusicPacket>().Select(packet => packet.Music).ToList();
    }
}
