using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Events;
using Moongate.Server.Data.Session;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Handlers;

public sealed class MobileHandlerTests
{
    [Test]
    public async Task HandleAsync_ForMobileAddedInSector_ShouldSendPacketsToOtherPlayersOnly()
    {
        var mobileId = (Serial)0x00000010u;
        var receiverId = (Serial)0x00000020u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var receiverSession = CreateSession(receiverId);
        sessions.Add(receiverSession);

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            PlayersInSector = [CreatePlayer(mobileId), CreatePlayer(receiverId)]
        };
        var characterService = new MobileHandlerTestCharacterService(CreatePlayer(mobileId));
        var handler = new MobileHandler(
            new NetworkServiceTestGameEventBusService(),
            spatial,
            characterService,
            sessions,
            queue
        );

        await handler.HandleAsync(new MobileAddedInSectorEvent(mobileId, 1, 100, 200));

        var packets = DequeueAll(queue);

        Assert.Multiple(
            () =>
            {
                Assert.That(packets, Has.Count.EqualTo(4));
                Assert.That(packets.All(packet => packet.SessionId == receiverSession.SessionId), Is.True);
                Assert.That(packets[0].Packet, Is.TypeOf<MobileIncomingPacket>());
                Assert.That(packets[1].Packet, Is.TypeOf<DrawPlayerPacket>());
                Assert.That(packets[2].Packet, Is.TypeOf<PlayerStatusPacket>());
                Assert.That(packets[3].Packet, Is.TypeOf<MobileDrawPacket>());
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForMobilePositionChanged_ShouldResolveTargetSectorAndSendPackets()
    {
        var mobileId = (Serial)0x00000100u;
        var receiverId = (Serial)0x00000200u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var receiverSession = CreateSession(receiverId);
        sessions.Add(receiverSession);

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SectorByLocation = new MapSector(1, 7, 8),
            PlayersInSector = [CreatePlayer(receiverId)]
        };
        var characterService = new MobileHandlerTestCharacterService(CreatePlayer(mobileId));
        var handler = new MobileHandler(
            new NetworkServiceTestGameEventBusService(),
            spatial,
            characterService,
            sessions,
            queue
        );

        await handler.HandleAsync(
            new MobilePositionChangedEvent(
                99,
                mobileId,
                1,
                new Point3D(200, 200, 0),
                new Point3D(210, 210, 0)
            )
        );

        var packets = DequeueAll(queue);

        Assert.Multiple(
            () =>
            {
                Assert.That(spatial.LastGetSectorMapId, Is.EqualTo(1));
                Assert.That(spatial.LastGetSectorLocation, Is.EqualTo(new Point3D(210, 210, 0)));
                Assert.That(packets, Has.Count.EqualTo(4));
                Assert.That(packets.All(packet => packet.SessionId == receiverSession.SessionId), Is.True);
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForMobilePositionChanged_ShouldNotSendPackets_WhenSectorNotFound()
    {
        var mobileId = (Serial)0x00000300u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var spatial = new MobileHandlerTestSpatialWorldService { SectorByLocation = null };
        var characterService = new MobileHandlerTestCharacterService(CreatePlayer(mobileId));
        var handler = new MobileHandler(
            new NetworkServiceTestGameEventBusService(),
            spatial,
            characterService,
            sessions,
            queue
        );

        await handler.HandleAsync(
            new MobilePositionChangedEvent(
                1,
                mobileId,
                1,
                new Point3D(0, 0, 0),
                new Point3D(1, 1, 0)
            )
        );

        Assert.That(queue.TryDequeue(out _), Is.False);
    }

    private static UOMobileEntity CreatePlayer(Serial id)
        => new()
        {
            Id = id,
            IsPlayer = true,
            Name = $"player-{id.Value}",
            Location = new Point3D(100, 100, 0),
            MapId = 1
        };

    private static GameSession CreateSession(Serial characterId)
    {
        var client = new MoongateTCPClient(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        return new GameSession(new GameNetworkSession(client))
        {
            CharacterId = characterId
        };
    }

    private static List<Moongate.Server.Data.Packets.OutgoingGamePacket> DequeueAll(
        BasePacketListenerTestOutgoingPacketQueue queue
    )
    {
        var packets = new List<Moongate.Server.Data.Packets.OutgoingGamePacket>();
        while (queue.TryDequeue(out var packet))
        {
            packets.Add(packet);
        }

        return packets;
    }

    private sealed class MobileHandlerTestCharacterService : ICharacterService
    {
        private readonly UOMobileEntity _mobile;

        public MobileHandlerTestCharacterService(UOMobileEntity mobile)
            => _mobile = mobile;

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => Task.FromResult(character.Id);

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult<UOMobileEntity?>(_mobile.Id == characterId ? _mobile : null);

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);
    }

    private sealed class MobileHandlerTestSpatialWorldService : ISpatialWorldService
    {
        public List<UOMobileEntity> PlayersInSector { get; set; } = [];

        public MapSector? SectorByLocation { get; set; }

        public int LastGetSectorMapId { get; private set; }

        public Point3D LastGetSectorLocation { get; private set; }

        public void AddOrUpdateMobile(UOMobileEntity mobile) { }

        public void AddOrUpdateItem(UOItemEntity item, int mapId) { }

        public void AddRegion(JsonRegion region) { }

        public void AddMusics(List<JsonMusic> musics) { }

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
            => [];

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
            => [];

        public List<GameSession> GetPlayersInRange(
            Point3D location,
            int range,
            int mapId,
            GameSession? excludeSession = null
        )
            => [];

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
            => PlayersInSector;

        public MapSector? GetSectorByLocation(int mapId, Point3D location)
        {
            LastGetSectorMapId = mapId;
            LastGetSectorLocation = location;

            return SectorByLocation;
        }

        public int GetMusic(Point3D location)
            => 0;

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation) { }

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation) { }

        public void RemoveEntity(Serial serial) { }

        public SectorSystemStats GetStats()
            => new();
    }
}
