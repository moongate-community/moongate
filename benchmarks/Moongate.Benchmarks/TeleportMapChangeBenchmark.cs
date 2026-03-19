using System.Net.Sockets;
using BenchmarkDotNet.Attributes;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Data.World;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.Events;
using Moongate.Server.Services.Spatial;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Benchmarks;

[MemoryDiagnoser, InvocationCount(1), WarmupCount(3), IterationCount(12)]
public class TeleportMapChangeBenchmark : IDisposable
{
    private const int SourceMapId = 1;
    private const int DestinationMapId = 2;
    private const int SameMapId = 0;
    private static readonly Point3D SourceLocation = new(5276, 1164, 0);
    private static readonly Point3D DestinationLocation = new(1518, 568, -14);
    private static readonly Point3D SameMapSourceLocation = new(5276, 1164, 0);
    private static readonly Point3D SameMapDestinationLocation = new(5304, 1184, 0);

    private readonly List<MoongateTCPClient> _clients = [];

    private BenchmarkScenario _crossMapScenario = null!;
    private BenchmarkScenario _sameMapScenario = null!;

    private sealed class NoOpGameEventBusService : IGameEventBusService
    {
        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
            => ValueTask.CompletedTask;

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener) where TEvent : IGameEvent { }
    }

    private sealed class NoOpTeleportersDataService : ITeleportersDataService
    {
        public IReadOnlyList<TeleporterEntry> GetAllEntries()
            => [];

        public IReadOnlyList<TeleporterEntry> GetEntriesBySourceMap(int mapId)
            => [];

        public IReadOnlyList<TeleporterEntry> GetEntriesBySourceSector(int mapId, int sectorX, int sectorY)
            => [];

        public void SetEntries(IReadOnlyList<TeleporterEntry> entries)
            => _ = entries;

        public bool TryGetEntryAtLocation(int mapId, Point3D location, out TeleporterEntry entry)
        {
            entry = default;

            return false;
        }

        public bool TryResolveTeleportDestination(
            int mapId,
            Point3D location,
            out int destinationMapId,
            out Point3D destinationLocation,
            int maxHops = 4
        )
        {
            _ = maxHops;
            destinationMapId = mapId;
            destinationLocation = location;

            return false;
        }
    }

    private sealed class BenchmarkGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly List<GameSession> _sessions = [];

        public int Count => _sessions.Count;

        public void Add(GameSession session)
            => _sessions.Add(session);

        public void Clear()
            => _sessions.Clear();

        public IReadOnlyCollection<GameSession> GetAll()
            => _sessions.ToArray();

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotSupportedException();

        public bool Remove(long sessionId)
            => _sessions.RemoveAll(session => session.SessionId == sessionId) > 0;

        public bool TryGet(long sessionId, out GameSession session)
        {
            session = _sessions.FirstOrDefault(item => item.SessionId == sessionId)!;

            return session is not null;
        }

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            session = _sessions.FirstOrDefault(item => item.CharacterId == characterId)!;

            return session is not null;
        }
    }

    private sealed class CountingOutgoingPacketQueue : IOutgoingPacketQueue
    {
        public int EnqueuedCount { get; private set; }

        public int CurrentQueueDepth => EnqueuedCount;

        public void Enqueue(long sessionId, IGameNetworkPacket packet)
        {
            _ = sessionId;
            _ = packet;
            EnqueuedCount++;
        }

        public void Reset()
            => EnqueuedCount = 0;

        public bool TryDequeue(out OutgoingGamePacket gamePacket)
        {
            gamePacket = default;

            return false;
        }
    }

    private sealed class BenchmarkSpeechService : ISpeechService
    {
        public int MessagesSent { get; private set; }

        public Task<int> BroadcastFromServerAsync(string text, short hue = 946, short font = 3, string language = "ENU")
            => Task.FromResult(0);

        public Task HandleOpenChatWindowAsync(
            GameSession session,
            OpenChatWindowPacket packet,
            CancellationToken cancellationToken = default
        )
            => Task.CompletedTask;

        public Task<UnicodeSpeechMessagePacket?> ProcessIncomingSpeechAsync(
            GameSession session,
            UnicodeSpeechPacket speechPacket,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<UnicodeSpeechMessagePacket?>(null);

        public void Reset()
            => MessagesSent = 0;

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
            MessagesSent++;

            return Task.FromResult(true);
        }

        public Task<int> SpeakAsMobileAsync(
            UOMobileEntity speaker,
            string text,
            int range = 12,
            ChatMessageType messageType = ChatMessageType.Regular,
            short hue = 0x3B2,
            short font = 3,
            string language = "ENU",
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(0);
    }

    private sealed class FixedLightService : ILightService
    {
        public int ComputeGlobalLightLevel(DateTime? utcNow = null)
        {
            _ = utcNow;

            return 0;
        }

        public int ComputeGlobalLightLevel(int mapId, Point3D location, DateTime? utcNow = null)
        {
            _ = mapId;
            _ = location;
            _ = utcNow;

            return 0;
        }

        public void SetGlobalLightOverride(int? lightLevel, bool applyImmediately = true)
        {
            _ = lightLevel;
            _ = applyImmediately;
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed record BenchmarkScenario(
        MobileHandler Handler,
        CountingOutgoingPacketQueue OutgoingQueue,
        BenchmarkSpeechService SpeechService,
        MobilePositionChangedEvent GameEvent
    );

    private sealed class BenchmarkCharacterService : ICharacterService
    {
        private readonly UOMobileEntity _character;
        private readonly UOItemEntity? _backpack;

        public BenchmarkCharacterService(UOMobileEntity character, UOItemEntity? backpack)
        {
            _character = character;
            _backpack = backpack;
        }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(false);

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => Task.CompletedTask;

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => Task.FromResult(character.Id);

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(_character.Id == character.Id ? _backpack : null);

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult<UOMobileEntity?>(_character.Id == characterId ? _character : null);

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(false);
    }

    private sealed class BenchmarkItemService : IItemService
    {
        public Dictionary<(int MapId, int SectorX, int SectorY), List<UOItemEntity>> ItemsBySector { get; } = [];

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => Task.FromResult(item.Id);

        public Task<bool> DeleteItemAsync(Serial itemId)
            => Task.FromResult(false);

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => Task.FromResult<DropItemToGroundResult?>(null);

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => Task.FromResult(false);

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(
                ItemsBySector.TryGetValue((mapId, sectorX, sectorY), out var items)
                    ? items
                    : new()
            );

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => Task.FromResult(false);

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(false);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromResult(new UOItemEntity());

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((false, (UOItemEntity?)null));

        public Task UpsertItemAsync(UOItemEntity item)
            => Task.CompletedTask;

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;
    }

    private sealed class BenchmarkMobileService : IMobileService
    {
        public Dictionary<(int MapId, int SectorX, int SectorY), List<UOMobileEntity>> MobilesBySector { get; } = [];

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult<UOMobileEntity?>(null);

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
            int mapId,
            int sectorX,
            int sectorY,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(
                MobilesBySector.TryGetValue((mapId, sectorX, sectorY), out var mobiles)
                    ? mobiles
                    : new()
            );

        public Task<UOMobileEntity> SpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(
                new UOMobileEntity
                {
                    Id = (Serial)0x7999_0001u,
                    Name = templateId,
                    Location = location,
                    MapId = mapId
                }
            );

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(
                (
                    true,
                    (UOMobileEntity?)new()
                    {
                        Id = (Serial)0x7999_0002u,
                        Name = templateId,
                        Location = location,
                        MapId = mapId
                    }
                )
            );
    }

    public void Dispose()
    {
        CleanupClients();
        GC.SuppressFinalize(this);
    }

    [Benchmark]
    public async Task<int> HandleCrossMapTeleport_ColdDestination()
    {
        _crossMapScenario.OutgoingQueue.Reset();
        _crossMapScenario.SpeechService.Reset();

        await _crossMapScenario.Handler.HandleAsync(_crossMapScenario.GameEvent);

        return _crossMapScenario.OutgoingQueue.EnqueuedCount + _crossMapScenario.SpeechService.MessagesSent;
    }

    [Benchmark]
    public async Task<int> HandleSameMapTeleport_ColdDestination_WithSelfRefresh()
    {
        _sameMapScenario.OutgoingQueue.Reset();
        _sameMapScenario.SpeechService.Reset();

        await _sameMapScenario.Handler.HandleAsync(_sameMapScenario.GameEvent);

        return _sameMapScenario.OutgoingQueue.EnqueuedCount + _sameMapScenario.SpeechService.MessagesSent;
    }

    [IterationCleanup]
    public void IterationCleanup()
        => CleanupClients();

    [IterationSetup]
    public void IterationSetup()
    {
        CleanupClients();
        _crossMapScenario = CreateScenario(
            DestinationMapId,
            DestinationLocation,
            SourceMapId,
            SourceLocation,
            DestinationMapId,
            DestinationLocation,
            [
                BuildObserver((Serial)0x7000_0002u, SourceMapId, SourceLocation),
                BuildObserver((Serial)0x7000_0003u, DestinationMapId, DestinationLocation)
            ],
            static (itemService, mobileService) =>
            {
                SeedOldWorld(itemService, mobileService);
                SeedDestinationWorld(itemService, mobileService);
            }
        );
        _sameMapScenario = CreateScenario(
            SameMapId,
            SameMapDestinationLocation,
            SameMapId,
            SameMapSourceLocation,
            SameMapId,
            SameMapDestinationLocation,
            [
                BuildObserver((Serial)0x7000_0012u, SameMapId, SameMapSourceLocation),
                BuildObserver((Serial)0x7000_0013u, SameMapId, SameMapDestinationLocation)
            ],
            static (itemService, mobileService) => SeedSameMapWorld(itemService, mobileService)
        );
    }

    private static UOItemEntity BuildBackpack()
    {
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x4000_1000u,
            Name = "benchmark-backpack",
            ItemId = 0x0E75,
            MapId = DestinationMapId,
            Location = DestinationLocation
        };

        backpack.AddItem(
            new UOItemEntity
            {
                Id = (Serial)0x4000_1001u,
                Name = "bandage",
                ItemId = 0x0E21,
                Amount = 25
            },
            new(42, 60)
        );

        return backpack;
    }

    private static UOItemEntity BuildEquippedItem(Serial id, ushort itemId, short hue)
        => new()
        {
            Id = id,
            ItemId = itemId,
            Hue = hue,
            Amount = 1
        };

    private static UOMobileEntity BuildMovingPlayer(int mapId, Point3D location)
        => new()
        {
            Id = (Serial)0x7000_0001u,
            Name = "benchmark-player",
            IsPlayer = true,
            MapId = mapId,
            Location = location,
            Strength = 50,
            Dexterity = 50,
            Intelligence = 25,
            Hits = 50,
            MaxHits = 50,
            Mana = 25,
            MaxMana = 25,
            Stamina = 40,
            MaxStamina = 40
        };

    private static UOMobileEntity BuildObserver(Serial id, int mapId, Point3D origin)
        => new()
        {
            Id = id,
            Name = $"observer-{id.Value}",
            IsPlayer = true,
            MapId = mapId,
            Location = new(origin.X + 4, origin.Y + 3, origin.Z),
            Hits = 50,
            MaxHits = 50
        };

    private void CleanupClients()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _clients.Clear();
    }

    private static MoongateConfig CreateBenchmarkConfig()
        => new()
        {
            Spatial = new()
            {
                SectorEnterSyncRadius = 3,
                SectorUpdateBroadcastRadius = 3,
                LazySectorEntityLoadRadius = 3
            }
        };

    private static UOItemEntity CreateGroundItem(Serial id, int sectorX, int sectorY, int mapId, string name)
        => new()
        {
            Id = id,
            Name = name,
            ItemId = 0x0EED,
            Amount = 1,
            MapId = mapId,
            ParentContainerId = Serial.Zero,
            EquippedMobileId = Serial.Zero,
            Location = new(
                (sectorX << MapSectorConsts.SectorShift) + 4,
                (sectorY << MapSectorConsts.SectorShift) + 5,
                0
            )
        };

    private static UOMobileEntity CreateNpc(Serial id, int sectorX, int sectorY, int mapId, string name)
        => new()
        {
            Id = id,
            Name = name,
            IsPlayer = false,
            MapId = mapId,
            Location = new(
                (sectorX << MapSectorConsts.SectorShift) + 7,
                (sectorY << MapSectorConsts.SectorShift) + 8,
                0
            ),
            Hits = 35,
            MaxHits = 35
        };

    private BenchmarkScenario CreateScenario(
        int currentMapId,
        Point3D currentLocation,
        int oldMapId,
        Point3D oldLocation,
        int newMapId,
        Point3D newLocation,
        IReadOnlyList<UOMobileEntity> observers,
        Action<BenchmarkItemService, BenchmarkMobileService> seedWorld
    )
    {
        var outgoingQueue = new CountingOutgoingPacketQueue();
        var speechService = new BenchmarkSpeechService();
        var sessions = new BenchmarkGameNetworkSessionService();
        var itemService = new BenchmarkItemService();
        var mobileService = new BenchmarkMobileService();
        var movingPlayer = BuildMovingPlayer(currentMapId, currentLocation);
        var backpack = BuildBackpack();
        movingPlayer.AddEquippedItem(ItemLayerType.Backpack, backpack);
        movingPlayer.BackpackId = backpack.Id;
        movingPlayer.AddEquippedItem(ItemLayerType.Shirt, BuildEquippedItem((Serial)0x4000_1010u, 0x1F7B, 0x0455));
        movingPlayer.AddEquippedItem(ItemLayerType.Pants, BuildEquippedItem((Serial)0x4000_1011u, 0x152E, 0x03E9));
        var characterService = new BenchmarkCharacterService(movingPlayer, backpack);

        seedWorld(itemService, mobileService);

        var spatialWorld = new SpatialWorldService(
            sessions,
            new NoOpGameEventBusService(),
            characterService,
            itemService,
            mobileService,
            outgoingQueue,
            new NoOpTeleportersDataService(),
            CreateBenchmarkConfig()
        );

        var movingSession = CreateSession(movingPlayer);
        sessions.Add(movingSession);

        foreach (var observer in observers)
        {
            sessions.Add(CreateSession(observer));
        }

        var dispatchEvents = new DispatchEventsService(spatialWorld, outgoingQueue, sessions);
        var handler = new MobileHandler(
            spatialWorld,
            characterService,
            speechService,
            dispatchEvents,
            sessions,
            outgoingQueue,
            CreateBenchmarkConfig(),
            new FixedLightService()
        );

        return new(
            handler,
            outgoingQueue,
            speechService,
            new(
                movingSession.SessionId,
                movingPlayer.Id,
                oldMapId,
                newMapId,
                oldLocation,
                newLocation,
                true
            )
        );
    }

    private GameSession CreateSession(UOMobileEntity character)
    {
        var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        _clients.Add(client);

        return new(new(client))
        {
            CharacterId = character.Id,
            Character = character,
            AccountType = AccountType.Regular
        };
    }

    private static void SeedArea(
        BenchmarkItemService itemService,
        BenchmarkMobileService mobileService,
        int mapId,
        Point3D center,
        uint serialSeed,
        string prefix
    )
    {
        var centerSectorX = center.X >> MapSectorConsts.SectorShift;
        var centerSectorY = center.Y >> MapSectorConsts.SectorShift;

        for (var sectorX = centerSectorX - 3; sectorX <= centerSectorX + 3; sectorX++)
        {
            for (var sectorY = centerSectorY - 3; sectorY <= centerSectorY + 3; sectorY++)
            {
                itemService.ItemsBySector[(mapId, sectorX, sectorY)] =
                [
                    CreateGroundItem((Serial)serialSeed++, sectorX, sectorY, mapId, $"{prefix}-item-a"),
                    CreateGroundItem((Serial)serialSeed++, sectorX, sectorY, mapId, $"{prefix}-item-b"),
                    CreateGroundItem((Serial)serialSeed++, sectorX, sectorY, mapId, $"{prefix}-item-c")
                ];

                mobileService.MobilesBySector[(mapId, sectorX, sectorY)] =
                [
                    CreateNpc((Serial)serialSeed++, sectorX, sectorY, mapId, $"{prefix}-npc-a"),
                    CreateNpc((Serial)serialSeed++, sectorX, sectorY, mapId, $"{prefix}-npc-b")
                ];
            }
        }
    }

    private static void SeedDestinationWorld(BenchmarkItemService itemService, BenchmarkMobileService mobileService)
    {
        var centerSectorX = DestinationLocation.X >> MapSectorConsts.SectorShift;
        var centerSectorY = DestinationLocation.Y >> MapSectorConsts.SectorShift;
        var serialSeed = 0x4200_0000u;

        for (var sectorX = centerSectorX - 3; sectorX <= centerSectorX + 3; sectorX++)
        {
            for (var sectorY = centerSectorY - 3; sectorY <= centerSectorY + 3; sectorY++)
            {
                itemService.ItemsBySector[(DestinationMapId, sectorX, sectorY)] =
                [
                    CreateGroundItem((Serial)serialSeed++, sectorX, sectorY, DestinationMapId, "dest-item-a"),
                    CreateGroundItem((Serial)serialSeed++, sectorX, sectorY, DestinationMapId, "dest-item-b"),
                    CreateGroundItem((Serial)serialSeed++, sectorX, sectorY, DestinationMapId, "dest-item-c")
                ];

                mobileService.MobilesBySector[(DestinationMapId, sectorX, sectorY)] =
                [
                    CreateNpc((Serial)serialSeed++, sectorX, sectorY, DestinationMapId, "dest-npc-a"),
                    CreateNpc((Serial)serialSeed++, sectorX, sectorY, DestinationMapId, "dest-npc-b")
                ];
            }
        }
    }

    private static void SeedOldWorld(BenchmarkItemService itemService, BenchmarkMobileService mobileService)
    {
        var centerSectorX = SourceLocation.X >> MapSectorConsts.SectorShift;
        var centerSectorY = SourceLocation.Y >> MapSectorConsts.SectorShift;
        var serialSeed = 0x4100_0000u;

        for (var sectorX = centerSectorX - 3; sectorX <= centerSectorX + 3; sectorX++)
        {
            for (var sectorY = centerSectorY - 3; sectorY <= centerSectorY + 3; sectorY++)
            {
                itemService.ItemsBySector[(SourceMapId, sectorX, sectorY)] =
                [
                    CreateGroundItem((Serial)serialSeed++, sectorX, sectorY, SourceMapId, "old-item-a"),
                    CreateGroundItem((Serial)serialSeed++, sectorX, sectorY, SourceMapId, "old-item-b")
                ];

                mobileService.MobilesBySector[(SourceMapId, sectorX, sectorY)] =
                [
                    CreateNpc((Serial)serialSeed++, sectorX, sectorY, SourceMapId, "old-npc")
                ];
            }
        }
    }

    private static void SeedSameMapWorld(BenchmarkItemService itemService, BenchmarkMobileService mobileService)
    {
        SeedArea(itemService, mobileService, SameMapId, SameMapSourceLocation, 0x4300_0000u, "same-old");
        SeedArea(itemService, mobileService, SameMapId, SameMapDestinationLocation, 0x4400_0000u, "same-dest");
    }
}
