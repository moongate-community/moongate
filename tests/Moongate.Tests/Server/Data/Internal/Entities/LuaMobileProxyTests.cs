using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Movement;
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

        public GameSession GetOrCreate(MoongateTCPClient client)
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

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
        {
            _ = item;
            _ = mapId;
        }

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => _ = mobile;

        public void AddRegion(JsonRegion region)
            => _ = region;

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

        public List<MapSector> GetActiveSectors()
            => [];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
        {
            _ = mapId;
            _ = centerSectorX;
            _ = centerSectorY;
            _ = radius;

            return [];
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

        public JsonRegion? GetRegionById(int regionId)
        {
            _ = regionId;

            return null;
        }

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
        public MobilePlayEffectEvent? LastMobilePlayEffectEvent { get; private set; }
        public MobileWarModeChangedEvent? LastMobileWarModeChangedEvent { get; private set; }

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
            else if (gameEvent is MobilePlayEffectEvent mobilePlayEffectEvent)
            {
                LastMobilePlayEffectEvent = mobilePlayEffectEvent;
            }
            else if (gameEvent is MobileWarModeChangedEvent mobileWarModeChangedEvent)
            {
                LastMobileWarModeChangedEvent = mobileWarModeChangedEvent;
            }

            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener) where TEvent : IGameEvent
            => _ = listener;
    }

    private sealed class LuaMobileProxyTestPathfindingService : IPathfindingService
    {
        public bool ShouldReturnPath { get; set; }

        public int Calls { get; private set; }

        public IReadOnlyList<DirectionType> PathToReturn { get; set; } = [];

        public bool TryFindPath(
            UOMobileEntity mobile,
            Point3D targetLocation,
            out IReadOnlyList<DirectionType> path,
            int maxVisitedNodes = 1024
        )
        {
            _ = mobile;
            _ = targetLocation;
            _ = maxVisitedNodes;
            Calls++;
            path = PathToReturn;

            return ShouldReturnPath;
        }
    }

    [Test]
    public void DisableWar_ShouldClearWarModeAndPublishEvent()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x1234u,
            MapId = 1,
            Location = new(100, 200, 5),
            IsWarMode = true
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
            null,
            null,
            gameEventBusService
        );

        var disabled = proxy.DisableWar();

        Assert.Multiple(
            () =>
            {
                Assert.That(disabled, Is.True);
                Assert.That(mobile.IsWarMode, Is.False);
                Assert.That(gameEventBusService.LastMobileWarModeChangedEvent.HasValue, Is.True);
                Assert.That(
                    gameEventBusService.LastMobileWarModeChangedEvent!.Value.Mobile.Id,
                    Is.EqualTo((Serial)0x1234u)
                );
                Assert.That(gameEventBusService.LastMobileWarModeChangedEvent!.Value.Mobile.IsWarMode, Is.False);
            }
        );
    }

    [Test]
    public void EnableWar_ShouldSetWarModeAndPublishEvent()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x1234u,
            MapId = 1,
            Location = new(100, 200, 5),
            IsWarMode = false
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
            null,
            null,
            gameEventBusService
        );

        var enabled = proxy.EnableWar();

        Assert.Multiple(
            () =>
            {
                Assert.That(enabled, Is.True);
                Assert.That(mobile.IsWarMode, Is.True);
                Assert.That(gameEventBusService.LastMobileWarModeChangedEvent.HasValue, Is.True);
                Assert.That(
                    gameEventBusService.LastMobileWarModeChangedEvent!.Value.Mobile.Id,
                    Is.EqualTo((Serial)0x1234u)
                );
                Assert.That(gameEventBusService.LastMobileWarModeChangedEvent!.Value.Mobile.IsWarMode, Is.True);
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
            Location = new(100, 200, 5)
        };
        var speechService = new LuaMobileProxyTestSpeechService();
        var gameNetworkSessionService = new LuaMobileProxyTestGameNetworkSessionService();
        var spatialWorldService = new LuaMobileProxyTestSpatialWorldService();
        var movementValidationService = new LuaMobileProxyTestMovementValidationService
        {
            ShouldSucceed = false,
            NewLocation = new(101, 200, 5)
        };
        var gameEventBusService = new LuaMobileProxyTestGameEventBusService();
        var proxy = new LuaMobileProxy(
            mobile,
            speechService,
            gameNetworkSessionService,
            spatialWorldService,
            movementValidationService,
            null,
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
    public void Move_WhenValidationSucceeds_ShouldUpdateLocationAndPublishEvent()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x1234u,
            MapId = 1,
            Location = new(100, 200, 5)
        };
        var speechService = new LuaMobileProxyTestSpeechService();
        var gameNetworkSessionService = new LuaMobileProxyTestGameNetworkSessionService();
        var spatialWorldService = new LuaMobileProxyTestSpatialWorldService();
        var movementValidationService = new LuaMobileProxyTestMovementValidationService
        {
            ShouldSucceed = true,
            NewLocation = new(101, 200, 5)
        };
        var gameEventBusService = new LuaMobileProxyTestGameEventBusService();
        var proxy = new LuaMobileProxy(
            mobile,
            speechService,
            gameNetworkSessionService,
            spatialWorldService,
            movementValidationService,
            null,
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
    public void MoveTowards_WhenPathExists_ShouldMoveOneStepAlongPath()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x1234u,
            MapId = 1,
            Location = new(100, 200, 5)
        };
        var targetMobile = new UOMobileEntity
        {
            Id = (Serial)0x5678u,
            MapId = 1,
            Location = new(101, 200, 5)
        };
        var speechService = new LuaMobileProxyTestSpeechService();
        var gameNetworkSessionService = new LuaMobileProxyTestGameNetworkSessionService();
        var spatialWorldService = new LuaMobileProxyTestSpatialWorldService();
        var movementValidationService = new LuaMobileProxyTestMovementValidationService
        {
            ShouldSucceed = true,
            NewLocation = new(101, 200, 5)
        };
        var pathfindingService = new LuaMobileProxyTestPathfindingService
        {
            ShouldReturnPath = true,
            PathToReturn = [DirectionType.East]
        };
        var gameEventBusService = new LuaMobileProxyTestGameEventBusService();
        var proxy = new LuaMobileProxy(
            mobile,
            speechService,
            gameNetworkSessionService,
            spatialWorldService,
            movementValidationService,
            pathfindingService,
            gameEventBusService
        );
        var targetProxy = new LuaMobileProxy(targetMobile, speechService, gameNetworkSessionService, spatialWorldService);

        proxy.MoveTowards(targetProxy);

        Assert.Multiple(
            () =>
            {
                Assert.That(pathfindingService.Calls, Is.EqualTo(1));
                Assert.That(mobile.Location, Is.EqualTo(new Point3D(101, 200, 5)));
                Assert.That(gameEventBusService.LastMobilePositionChangedEvent.HasValue, Is.True);
                Assert.That(
                    gameEventBusService.LastMobilePositionChangedEvent!.Value.NewLocation,
                    Is.EqualTo(new Point3D(101, 200, 5))
                );
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
            Location = new(100, 200, 5)
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
            null,
            null,
            gameEventBusService
        );

        proxy.PlaySound(0x0210);

        Assert.Multiple(
            () =>
            {
                Assert.That(gameEventBusService.LastMobilePlaySoundEvent.HasValue, Is.True);
                Assert.That(gameEventBusService.LastMobilePlaySoundEvent!.Value.MobileId, Is.EqualTo((Serial)0x1234u));
                Assert.That(gameEventBusService.LastMobilePlaySoundEvent!.Value.MapId, Is.EqualTo(1));
                Assert.That(
                    gameEventBusService.LastMobilePlaySoundEvent!.Value.Location,
                    Is.EqualTo(new Point3D(100, 200, 5))
                );
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
            Location = new(100, 200, 5)
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

    [Test]
    public void SetEffect_ShouldPublishMobilePlayEffectEvent()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x1234u,
            MapId = 1,
            Location = new(100, 200, 5)
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
            null,
            null,
            gameEventBusService
        );

        proxy.SetEffect(0x3728, effect: 2023);

        Assert.Multiple(
            () =>
            {
                Assert.That(gameEventBusService.LastMobilePlayEffectEvent.HasValue, Is.True);
                Assert.That(gameEventBusService.LastMobilePlayEffectEvent!.Value.MobileId, Is.EqualTo((Serial)0x1234u));
                Assert.That(gameEventBusService.LastMobilePlayEffectEvent!.Value.MapId, Is.EqualTo(1));
                Assert.That(
                    gameEventBusService.LastMobilePlayEffectEvent!.Value.Location,
                    Is.EqualTo(new Point3D(100, 200, 5))
                );
                Assert.That(gameEventBusService.LastMobilePlayEffectEvent!.Value.ItemId, Is.EqualTo(0x3728));
                Assert.That(gameEventBusService.LastMobilePlayEffectEvent!.Value.Speed, Is.EqualTo(10));
                Assert.That(gameEventBusService.LastMobilePlayEffectEvent!.Value.Duration, Is.EqualTo(10));
                Assert.That(gameEventBusService.LastMobilePlayEffectEvent!.Value.Effect, Is.EqualTo(2023));
            }
        );
    }

    [Test]
    public void SetWarMode_WhenFalse_ShouldDisableWarAndPublishEvent()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x1234u,
            MapId = 1,
            Location = new(100, 200, 5),
            IsWarMode = true
        };
        var proxy = new LuaMobileProxy(
            mobile,
            new LuaMobileProxyTestSpeechService(),
            new LuaMobileProxyTestGameNetworkSessionService(),
            new LuaMobileProxyTestSpatialWorldService(),
            null,
            null,
            new LuaMobileProxyTestGameEventBusService()
        );

        var changed = proxy.SetWarMode(false);

        Assert.Multiple(
            () =>
            {
                Assert.That(changed, Is.True);
                Assert.That(mobile.IsWarMode, Is.False);
            }
        );
    }

    [Test]
    public void SetWarMode_WhenTrue_ShouldEnableWarAndPublishEvent()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x1234u,
            MapId = 1,
            Location = new(100, 200, 5),
            IsWarMode = false
        };
        var proxy = new LuaMobileProxy(
            mobile,
            new LuaMobileProxyTestSpeechService(),
            new LuaMobileProxyTestGameNetworkSessionService(),
            new LuaMobileProxyTestSpatialWorldService(),
            null,
            null,
            new LuaMobileProxyTestGameEventBusService()
        );

        var changed = proxy.SetWarMode(true);

        Assert.Multiple(
            () =>
            {
                Assert.That(changed, Is.True);
                Assert.That(mobile.IsWarMode, Is.True);
            }
        );
    }

    [Test]
    public void Teleport_ShouldUpdateMapAndLocationAndPublishPositionEvent()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x1234u,
            MapId = 1,
            Location = new(100, 200, 5)
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
            null,
            null,
            gameEventBusService
        );

        var teleported = proxy.Teleport(2, 4500, 1300, 20);

        Assert.Multiple(
            () =>
            {
                Assert.That(teleported, Is.True);
                Assert.That(mobile.MapId, Is.EqualTo(2));
                Assert.That(mobile.Location, Is.EqualTo(new Point3D(4500, 1300, 20)));
                Assert.That(gameEventBusService.LastMobilePositionChangedEvent.HasValue, Is.True);
                Assert.That(gameEventBusService.LastMobilePositionChangedEvent!.Value.MapId, Is.EqualTo(2));
                Assert.That(
                    gameEventBusService.LastMobilePositionChangedEvent!.Value.OldLocation,
                    Is.EqualTo(new Point3D(100, 200, 5))
                );
                Assert.That(
                    gameEventBusService.LastMobilePositionChangedEvent!.Value.NewLocation,
                    Is.EqualTo(new Point3D(4500, 1300, 20))
                );
            }
        );
    }

    [Test]
    public void Teleport_WhenOnlyMapChanges_ShouldPublishPositionEvent()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x1234u,
            MapId = 1,
            Location = new(100, 200, 5)
        };
        var gameEventBusService = new LuaMobileProxyTestGameEventBusService();
        var proxy = new LuaMobileProxy(
            mobile,
            new LuaMobileProxyTestSpeechService(),
            new LuaMobileProxyTestGameNetworkSessionService(),
            new LuaMobileProxyTestSpatialWorldService(),
            null,
            null,
            gameEventBusService
        );

        var teleported = proxy.Teleport(2, 100, 200, 5);

        Assert.Multiple(
            () =>
            {
                Assert.That(teleported, Is.True);
                Assert.That(mobile.MapId, Is.EqualTo(2));
                Assert.That(mobile.Location, Is.EqualTo(new Point3D(100, 200, 5)));
                Assert.That(gameEventBusService.LastMobilePositionChangedEvent.HasValue, Is.True);
                Assert.That(gameEventBusService.LastMobilePositionChangedEvent!.Value.OldMapId, Is.EqualTo(1));
                Assert.That(gameEventBusService.LastMobilePositionChangedEvent!.Value.MapId, Is.EqualTo(2));
            }
        );
    }
}
