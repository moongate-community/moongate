using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Internal.Movement;
using Moongate.Server.Ultima.Data.Movement;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Tests.TestSupport.Ultima.Regions;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class TeleportServiceTests : IAsyncLifetime
{
    private readonly RecordingWorldViewService _view = new();
    private readonly StubBankService _bank = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private TeleportService _teleports = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var aria));
        _aria = aria;
        _aria.Body = 0x0190;
        _aria.Direction = DirectionType.South;
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1600, 1600, 0)));
        _teleports = new(_fixture.Mobiles, _view, _fixture.Sessions, _fixture.Sender, _fixture.Sectors, _bank);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void Teleport_APlayer_MovesItAndTellsItsClientWhereItStands()
    {
        Assert.True(_teleports.Teleport(_aria, MapType.Trammel, new Point3D(5690, 569, 25)));

        Assert.Equal(new Point3D(5690, 569, 25), _aria.Location);
        var update = Assert.IsType<MobileUpdatePacket>(Assert.Single(_fixture.Sender.Sent));
        Assert.Equal((_aria.Id, new Point3D(5690, 569, 25), DirectionType.South), (update.Serial, update.Location, update.Direction));
        Assert.Equal([_session.SessionId], _fixture.Sender.SentSessionIds);
    }

    [Fact]
    public void Teleport_TellsThePlayersAroundAfterTheMoversOwnClient()
    {
        _view.OnCall = _ => Assert.Single(_fixture.Sender.Sent);

        _teleports.Teleport(_aria, MapType.Trammel, new Point3D(5690, 569, 25));

        Assert.Equal(["Teleported 2 Trammel 1600,1600,0"], _view.Calls);
    }

    [Fact]
    public async Task Teleport_APlayer_StartsItsStepSequenceAndStepTimerAgain()
    {
        var state = new MovementState { ExpectedSequence = 42, NextStepAt = 999 };
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(MovementSessionKeys.State, state));

        _teleports.Teleport(_aria, MapType.Trammel, new Point3D(5690, 569, 25));

        Assert.Equal((0, 0L), (state.ExpectedSequence, state.NextStepAt));
    }

    [Fact]
    public void Teleport_AnNpc_OnlyTellsThePlayersAround()
    {
        var orc = new MobileEntity { Id = new Serial(9), Name = "an orc", Map = MapType.Trammel, Location = new Point3D(1601, 1600, 0) };
        _fixture.Mobiles.EnterWorld(orc);

        Assert.True(_teleports.Teleport(orc, MapType.Trammel, new Point3D(5690, 569, 25)));

        Assert.Empty(_fixture.Sender.Sent);
        Assert.Equal(["Teleported 9 Trammel 1601,1600,0"], _view.Calls);
    }

    [Fact]
    public void Teleport_ToAnotherMap_SendsTheMapChangeBeforeTheNewPosition()
    {
        Assert.True(_teleports.Teleport(_aria, MapType.Felucca, new Point3D(5690, 569, 25)));

        Assert.Equal((MapType.Felucca, new Point3D(5690, 569, 25)), (_aria.Map, _aria.Location));
        Assert.Equal([typeof(MapChangePacket), typeof(MobileUpdatePacket)], _fixture.Sender.Sent.Select(packet => packet.GetType()));
        Assert.Equal(MapType.Felucca, ((MapChangePacket)_fixture.Sender.Sent[0]).Map);
        Assert.Equal(["Teleported 2 Trammel 1600,1600,0"], _view.Calls);
    }

    [Fact]
    public void Teleport_ToAnotherMap_SendsTheMapChangeBeforeTheRegionListenersSendTheirs()
    {
        // The season, the light and the weather are sent by the listeners of the region change.
        var listener = new RecordingRegionChangeListener();
        var regions = new RegionService(new StubDataLoaderService(), new Lazy<IEnumerable<IRegionChangeListener>>(() => [listener]));
        var mobiles = new MobileService(new StubMovementService(), _fixture.Sectors, regions: regions);
        var aria = new MobileEntity
        {
            Id = new Serial(2), AccountId = new Serial(0x42), Name = "Aria", Map = MapType.Trammel,
            Location = new Point3D(1600, 1600, 0)
        };
        mobiles.EnterWorld(aria);
        listener.Changes.Clear();
        listener.OnChange = () => Assert.IsType<MapChangePacket>(Assert.Single(_fixture.Sender.Sent));

        Assert.True(new TeleportService(mobiles, _view, _fixture.Sessions, _fixture.Sender, _fixture.Sectors, _bank)
            .Teleport(aria, MapType.Felucca, new Point3D(5690, 569, 25)));

        Assert.Equal(["Aria: - -> -"], listener.Changes);
    }

    [Fact]
    public void Teleport_AnNpcToAnotherMap_SendsNoMapChange()
    {
        var orc = new MobileEntity { Id = new Serial(9), Name = "an orc", Map = MapType.Trammel, Location = new Point3D(1601, 1600, 0) };
        _fixture.Mobiles.EnterWorld(orc);

        Assert.True(_teleports.Teleport(orc, MapType.Felucca, new Point3D(5690, 569, 25)));

        Assert.Empty(_fixture.Sender.Sent);
        Assert.Equal(["Teleported 9 Trammel 1601,1600,0"], _view.Calls);
    }

    [Fact]
    public void Teleport_OnItsMap_ClosesTheMobilesBank()
    {
        Assert.True(_teleports.Teleport(_aria, MapType.Trammel, new Point3D(5690, 569, 25)));

        Assert.Equal([_aria], _bank.Closed);
    }

    [Fact]
    public void Teleport_ToTheSpotItStandsOn_ClosesTheMobilesBank()
    {
        // The bank is open on a spot: without this a teleport back to it, with no step between, finds it open.
        Assert.True(_teleports.Teleport(_aria, MapType.Trammel, new Point3D(1600, 1600, 0)));

        Assert.Equal([_aria], _bank.Closed);
    }

    [Fact]
    public void Teleport_ToAnotherMap_ClosesTheMobilesBank()
    {
        Assert.True(_teleports.Teleport(_aria, MapType.Felucca, new Point3D(5690, 569, 25)));

        Assert.Equal([_aria], _bank.Closed);
    }

    [Fact]
    public void Teleport_ThatDoesNotHappen_LeavesTheBankAsItIs()
    {
        Assert.False(_teleports.Teleport(_aria, MapType.Trammel, new Point3D(-5, 10, 0)));

        Assert.Empty(_bank.Closed);
    }

    [Fact]
    public void Teleport_ToAMapThatIsNotLoaded_ChangesAndSendsNothing()
    {
        Assert.False(_teleports.Teleport(_aria, MapType.Tokuno, new Point3D(100, 100, 0)));

        Assert.Equal(MapType.Trammel, _aria.Map);
        Assert.Empty(_fixture.Sender.Sent);
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public void Teleport_OutsideTheMap_ChangesAndSendsNothing()
    {
        Assert.False(_teleports.Teleport(_aria, MapType.Trammel, new Point3D(-5, 10, 0)));

        Assert.Equal(new Point3D(1600, 1600, 0), _aria.Location);
        Assert.Empty(_fixture.Sender.Sent);
        Assert.Empty(_view.Calls);
    }
}
