using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Data.Internal.Entities;

public sealed class LuaMobileProxyTests
{
    [Test]
    public void Move_WhenValidationSucceeds_ShouldUpdateLocationAndPublishEvent()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x1234u,
            MapId = 1,
            Location = new Point3D(100, 200, 5)
        };
        var speechService = new LuaMobileProxyTestSpeechService();
        var gameNetworkSessionService = new LuaMobileProxyTestGameNetworkSessionService();
        var spatialWorldService = new LuaMobileProxyTestSpatialWorldService();
        var movementValidationService = new LuaMobileProxyTestMovementValidationService
        {
            ShouldSucceed = true,
            NewLocation = new Point3D(101, 200, 5)
        };
        var gameEventBusService = new LuaMobileProxyTestGameEventBusService();
        var proxy = new LuaMobileProxy(
            mobile,
            speechService,
            gameNetworkSessionService,
            spatialWorldService,
            movementValidationService,
            gameEventBusService
        );

        var moved = proxy.Move(DirectionType.East);

        Assert.Multiple(
            () =>
            {
                Assert.That(moved, Is.True);
                Assert.That(mobile.Location, Is.EqualTo(new Point3D(101, 200, 5)));
                Assert.That(mobile.Direction, Is.EqualTo(DirectionType.East));
                Assert.That(gameEventBusService.LastMobilePositionChangedEvent.HasValue, Is.True);
                Assert.That(
                    gameEventBusService.LastMobilePositionChangedEvent!.Value.OldLocation,
                    Is.EqualTo(new Point3D(100, 200, 5))
                );
                Assert.That(
                    gameEventBusService.LastMobilePositionChangedEvent!.Value.NewLocation,
                    Is.EqualTo(new Point3D(101, 200, 5))
                );
            }
        );
    }

    [Test]
    public void Move_WhenValidationFails_ShouldNotUpdateLocationOrPublishEvent()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x1234u,
            MapId = 1,
            Location = new Point3D(100, 200, 5)
        };
        var speechService = new LuaMobileProxyTestSpeechService();
        var gameNetworkSessionService = new LuaMobileProxyTestGameNetworkSessionService();
        var spatialWorldService = new LuaMobileProxyTestSpatialWorldService();
        var movementValidationService = new LuaMobileProxyTestMovementValidationService
        {
            ShouldSucceed = false,
            NewLocation = new Point3D(101, 200, 5)
        };
        var gameEventBusService = new LuaMobileProxyTestGameEventBusService();
        var proxy = new LuaMobileProxy(
            mobile,
            speechService,
            gameNetworkSessionService,
            spatialWorldService,
            movementValidationService,
            gameEventBusService
        );

        var moved = proxy.Move(DirectionType.East);

        Assert.Multiple(
            () =>
            {
                Assert.That(moved, Is.False);
                Assert.That(mobile.Location, Is.EqualTo(new Point3D(100, 200, 5)));
                Assert.That(gameEventBusService.LastMobilePositionChangedEvent.HasValue, Is.False);
            }
        );
    }

    [Test]
    public void PlaySound_ShouldPublishMobilePlaySoundEvent()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x1234u,
            MapId = 1,
            Location = new Point3D(100, 200, 5)
        };
        var speechService = new LuaMobileProxyTestSpeechService();
        var gameNetworkSessionService = new LuaMobileProxyTestGameNetworkSessionService();
        var spatialWorldService = new LuaMobileProxyTestSpatialWorldService();
        var gameEventBusService = new LuaMobileProxyTestGameEventBusService();
        var proxy = new LuaMobileProxy(
            mobile,
            speechService,
            gameNetworkSessionService,
            spatialWorldService,
            movementValidationService: null,
            gameEventBusService
        );

        proxy.PlaySound(0x0210);

        Assert.Multiple(
            () =>
            {
                Assert.That(gameEventBusService.LastMobilePlaySoundEvent.HasValue, Is.True);
                Assert.That(gameEventBusService.LastMobilePlaySoundEvent!.Value.MobileId, Is.EqualTo((Serial)0x1234u));
                Assert.That(gameEventBusService.LastMobilePlaySoundEvent!.Value.MapId, Is.EqualTo(1));
                Assert.That(gameEventBusService.LastMobilePlaySoundEvent!.Value.Location, Is.EqualTo(new Point3D(100, 200, 5)));
                Assert.That(gameEventBusService.LastMobilePlaySoundEvent!.Value.Mode, Is.EqualTo(0x01));
                Assert.That(gameEventBusService.LastMobilePlaySoundEvent!.Value.SoundModel, Is.EqualTo(0x0210));
                Assert.That(gameEventBusService.LastMobilePlaySoundEvent!.Value.Unknown3, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public void Say_ShouldUseSpeakAsMobileAsync()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x1234u,
            MapId = 1,
            Location = new Point3D(100, 200, 5)
        };
        var speechService = new LuaMobileProxyTestSpeechService { SpeakAsMobileResult = 2 };
        var gameNetworkSessionService = new LuaMobileProxyTestGameNetworkSessionService();
        var spatialWorldService = new LuaMobileProxyTestSpatialWorldService();
        var proxy = new LuaMobileProxy(mobile, speechService, gameNetworkSessionService, spatialWorldService);

        var sent = proxy.Say("miaow");

        Assert.Multiple(
            () =>
            {
                Assert.That(sent, Is.True);
                Assert.That(speechService.LastSpeakAsMobileCallCount, Is.EqualTo(1));
                Assert.That(speechService.LastSpeaker, Is.SameAs(mobile));
                Assert.That(speechService.LastSpokenText, Is.EqualTo("miaow"));
            }
        );
    }

    private sealed class LuaMobileProxyTestSpeechService : ISpeechService
    {
        public int LastSpeakAsMobileCallCount { get; private set; }

        public UOMobileEntity? LastSpeaker { get; private set; }

        public string? LastSpokenText { get; private set; }

        public int SpeakAsMobileResult { get; set; } = 1;

        public Task<int> BroadcastFromServerAsync(
            string text,
            short hue = 946,
            short font = 3,
            string language = "ENU"
        )
        {
            _ = text;
            _ = hue;
            _ = font;
            _ = language;
            return Task.FromResult(0);
        }

        public Task HandleOpenChatWindowAsync(
            GameSession session,
            OpenChatWindowPacket packet,
            CancellationToken cancellationToken = default
        )
        {
            _ = session;
            _ = packet;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task<UnicodeSpeechMessagePacket?> ProcessIncomingSpeechAsync(
            GameSession session,
            UnicodeSpeechPacket speechPacket,
            CancellationToken cancellationToken = default
        )
        {
            _ = session;
            _ = speechPacket;
            _ = cancellationToken;
            return Task.FromResult<UnicodeSpeechMessagePacket?>(null);
        }

        public Task<bool> SendMessageFromServerAsync(
            GameSession session,
            string text,
            short hue = 946,
            short font = 3,
            string language = "ENU"
        )
        {
            _ = session;
            _ = text;
            _ = hue;
            _ = font;
            _ = language;
            return Task.FromResult(true);
        }

        public Task<int> SpeakAsMobileAsync(
            UOMobileEntity speaker,
            string text,
            int range = 12,
            ChatMessageType messageType = ChatMessageType.Regular,
            short hue = SpeechHues.Default,
            short font = SpeechHues.DefaultFont,
            string language = "ENU",
            CancellationToken cancellationToken = default
        )
        {
            _ = range;
            _ = messageType;
            _ = hue;
            _ = font;
            _ = language;
            _ = cancellationToken;
            LastSpeakAsMobileCallCount++;
            LastSpeaker = speaker;
            LastSpokenText = text;
            return Task.FromResult(SpeakAsMobileResult);
        }
    }

    private sealed class LuaMobileProxyTestGameNetworkSessionService : IGameNetworkSessionService
    {
        public int Count => 0;

        public void Clear() { }

        public IReadOnlyCollection<GameSession> GetAll()
            => [];

        public GameSession GetOrCreate(Moongate.Network.Client.MoongateTCPClient client)
        {
            _ = client;
            throw new NotImplementedException();
        }

        public bool Remove(long sessionId)
        {
            _ = sessionId;
            return false;
        }

        public bool TryGet(long sessionId, out GameSession session)
        {
            _ = sessionId;
            session = null!;
            return false;
        }

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            _ = characterId;
            session = null!;
            return false;
        }
    }

    private sealed class LuaMobileProxyTestSpatialWorldService : ISpatialWorldService
    {
        public int BroadcastCallCount { get; private set; }

        public IGameNetworkPacket? LastPacket { get; private set; }

        public int LastMapId { get; private set; }

        public Point3D LastLocation { get; private set; } = Point3D.Zero;

        public int? LastRange { get; private set; }

        public Task<int> BroadcastToPlayersAsync(
            IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
        {
            _ = excludeSessionId;
            BroadcastCallCount++;
            LastPacket = packet;
            LastMapId = mapId;
            LastLocation = location;
            LastRange = range;
            return Task.FromResult(1);
        }

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
        {
            _ = item;
            _ = mapId;
        }

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => _ = mobile;

        public void AddRegion(JsonRegion region)
            => _ = region;

        public JsonRegion? GetRegionById(int regionId)
        {
            _ = regionId;
            return null;
        }

        public int GetMusic(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;
            return 0;
        }

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
        {
            _ = location;
            _ = range;
            _ = mapId;
            return [];
        }

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
        {
            _ = location;
            _ = range;
            _ = mapId;
            return [];
        }

        public List<GameSession> GetPlayersInRange(
            Point3D location,
            int range,
            int mapId,
            GameSession? excludeSession = null
        )
        {
            _ = location;
            _ = range;
            _ = mapId;
            _ = excludeSession;
            return [];
        }

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
        {
            _ = mapId;
            _ = sectorX;
            _ = sectorY;
            return [];
        }

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
        {
            _ = mapId;
            _ = centerSectorX;
            _ = centerSectorY;
            _ = radius;
            return [];
        }

        public List<MapSector> GetActiveSectors()
            => [];

        public MapSector? GetSectorByLocation(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;
            return null;
        }

        public SectorSystemStats GetStats()
            => new();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation)
        {
            _ = item;
            _ = mapId;
            _ = oldLocation;
            _ = newLocation;
        }

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
        {
            _ = mobile;
            _ = oldLocation;
            _ = newLocation;
        }

        public void RemoveEntity(Serial serial)
            => _ = serial;
    }

    private sealed class LuaMobileProxyTestMovementValidationService : IMovementValidationService
    {
        public bool ShouldSucceed { get; set; }

        public Point3D NewLocation { get; set; } = Point3D.Zero;

        public bool TryResolveMove(UOMobileEntity mobile, DirectionType direction, out Point3D newLocation)
        {
            _ = mobile;
            _ = direction;
            newLocation = NewLocation;
            return ShouldSucceed;
        }
    }

    private sealed class LuaMobileProxyTestGameEventBusService : IGameEventBusService
    {
        public MobilePositionChangedEvent? LastMobilePositionChangedEvent { get; private set; }
        public MobilePlaySoundEvent? LastMobilePlaySoundEvent { get; private set; }

        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
        {
            _ = cancellationToken;

            if (gameEvent is MobilePositionChangedEvent mobilePositionChangedEvent)
            {
                LastMobilePositionChangedEvent = mobilePositionChangedEvent;
            }
            else if (gameEvent is MobilePlaySoundEvent mobilePlaySoundEvent)
            {
                LastMobilePlaySoundEvent = mobilePlaySoundEvent;
            }

            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener) where TEvent : IGameEvent
            => _ = listener;
    }
}
