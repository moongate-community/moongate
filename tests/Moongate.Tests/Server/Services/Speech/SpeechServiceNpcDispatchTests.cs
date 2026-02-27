using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Session;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Services.Events;
using Moongate.Server.Services.Speech;
using Moongate.Server.Types.Commands;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Speech;

public sealed class SpeechServiceNpcDispatchTests
{
    private sealed class SpeechServiceNpcDispatchCommandSystemService : ICommandSystemService
    {
        public Task ExecuteCommandAsync(
            string commandWithArgs,
            CommandSourceType source = CommandSourceType.Console,
            GameSession? session = null,
            CancellationToken cancellationToken = default
        )
        {
            _ = commandWithArgs;
            _ = source;
            _ = session;
            _ = cancellationToken;

            return Task.CompletedTask;
        }

        public void RegisterCommand(
            string commandName,
            Func<CommandSystemContext, Task> handler,
            string? description = null,
            CommandSourceType source = CommandSourceType.Console,
            AccountType minimumAccountType = AccountType.Administrator,
            Func<CommandAutocompleteContext, IReadOnlyList<string>>? autocompleteProvider = null
        )
        {
            _ = commandName;
            _ = handler;
            _ = description;
            _ = source;
            _ = minimumAccountType;
            _ = autocompleteProvider;
        }

        public IReadOnlyList<string> GetAutocompleteSuggestions(string commandWithArgs)
        {
            _ = commandWithArgs;

            return [];
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class SpeechServiceNpcDispatchSpatialWorldService : ISpatialWorldService
    {
        public List<UOMobileEntity> NearbyMobiles { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
        {
            _ = item;
            _ = mapId;
        }

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => NearbyMobiles.Add(mobile);

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

            return NearbyMobiles;
        }

        public List<GameSession> GetPlayersInRange(Point3D location, int range, int mapId, GameSession? excludeSession = null)
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

    private sealed class SpeechHeardEventListener : IGameEventListener<SpeechHeardEvent>
    {
        public List<SpeechHeardEvent> Events { get; } = [];

        public Task HandleAsync(SpeechHeardEvent gameEvent, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            Events.Add(gameEvent);

            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task ProcessIncomingSpeechAsync_ShouldPublishSpeechHeardEvent_ForNearbyNpc()
    {
        var commandSystemService = new SpeechServiceNpcDispatchCommandSystemService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new SpeechServiceTestGameNetworkSessionService();
        var eventBus = new GameEventBusService();
        var spatial = new SpeechServiceNpcDispatchSpatialWorldService();
        var listener = new SpeechHeardEventListener();
        eventBus.RegisterListener(listener);

        spatial.NearbyMobiles.Add(
            new()
            {
                Id = (Serial)0x00000010,
                Name = "orc",
                IsPlayer = false,
                MapId = 1,
                Location = new Point3D(100, 100, 0)
            }
        );

        var speechService = new SpeechService(
            commandSystemService,
            outgoingPacketQueue,
            sessionService,
            eventBus,
            spatial
        );

        var session = new GameSession(null)
        {
            Character = new()
            {
                Id = (Serial)0x00000002,
                Name = "player",
                MapId = 1,
                Location = new Point3D(101, 101, 0)
            },
            CharacterId = (Serial)0x00000002
        };

        var packet = new UnicodeSpeechPacket
        {
            MessageType = ChatMessageType.Regular,
            Hue = 0x0035,
            Font = 0x0003,
            Language = "ENU",
            Text = "hello npc"
        };

        _ = await speechService.ProcessIncomingSpeechAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(listener.Events.Count, Is.EqualTo(1));
                Assert.That(listener.Events[0].ListenerNpcId, Is.EqualTo((Serial)0x00000010));
                Assert.That(listener.Events[0].SpeakerId, Is.EqualTo((Serial)0x00000002));
                Assert.That(listener.Events[0].Text, Is.EqualTo("hello npc"));
                Assert.That(listener.Events[0].MapId, Is.EqualTo(1));
                Assert.That(listener.Events[0].BaseEvent.Timestamp, Is.GreaterThan(0));
            }
        );
    }
}
