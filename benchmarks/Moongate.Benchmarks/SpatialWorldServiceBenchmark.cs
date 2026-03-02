using BenchmarkDotNet.Attributes;
using Moongate.Network.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Services.Spatial;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Benchmarks;

[MemoryDiagnoser]
public class SpatialWorldServiceBenchmark
{
    private readonly List<UOMobileEntity> _mobiles = [];
    private readonly List<(UOMobileEntity Mobile, Point3D OldLocation, Point3D NewLocation)> _moves = [];

    private SpatialWorldService _service = null!;

    [Params(500, 2000)]
    public int MobileCount { get; set; }

    [Benchmark]
    public int AddOrUpdateMobiles()
    {
        for (var i = 0; i < _mobiles.Count; i++)
        {
            _service.AddOrUpdateMobile(_mobiles[i]);
        }

        return _service.GetStats().TotalEntities;
    }

    [Benchmark]
    public int MoveMobilesAcrossSectors()
    {
        for (var i = 0; i < _moves.Count; i++)
        {
            var move = _moves[i];
            _service.OnMobileMoved(move.Mobile, move.OldLocation, move.NewLocation);
        }

        return _service.GetStats().TotalSectors;
    }

    [Benchmark]
    public int GetPlayersInHotSector()
    {
        var players = _service.GetPlayersInSector(0, 217, 162);

        return players.Count;
    }

    [GlobalSetup]
    public void Setup()
    {
        _service = new(
            new EmptyGameNetworkSessionService(),
            new NoOpGameEventBusService(),
            new NoOpCharacterService(),
            new NoOpItemService(),
            new NoOpMobileService(),
            new NoOpOutgoingPacketQueue(),
            new()
            {
                Spatial = new()
                {
                    LazySectorItemLoadEnabled = false,
                    LazySectorEntityLoadRadius = 0,
                    SectorWarmupRadius = 0,
                    SectorEnterSyncRadius = 3
                }
            }
        );

        _mobiles.Clear();
        _moves.Clear();

        var side = (int)Math.Ceiling(Math.Sqrt(MobileCount));
        var index = 0;

        for (var x = 0; x < side && index < MobileCount; x++)
        {
            for (var y = 0; y < side && index < MobileCount; y++)
            {
                var oldLocation = new Point3D(3470 + x, 2590 + y, 0);
                var mobile = new UOMobileEntity
                {
                    Id = (Serial)(uint)(index + 1),
                    Name = $"player_{index}",
                    IsPlayer = true,
                    MapId = 0,
                    Location = oldLocation,
                    Direction = DirectionType.North
                };

                _mobiles.Add(mobile);
                _moves.Add((mobile, oldLocation, new Point3D(oldLocation.X + 1, oldLocation.Y, oldLocation.Z)));
                index++;
            }
        }

        for (var i = 0; i < _mobiles.Count; i++)
        {
            _service.AddOrUpdateMobile(_mobiles[i]);
        }
    }

    private sealed class NoOpGameEventBusService : IGameEventBusService
    {
        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
            => ValueTask.CompletedTask;

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener) where TEvent : IGameEvent
        {
        }
    }

    private sealed class NoOpOutgoingPacketQueue : IOutgoingPacketQueue
    {
        public int CurrentQueueDepth => 0;

        public void Enqueue(long sessionId, IGameNetworkPacket packet)
        {
        }

        public bool TryDequeue(out OutgoingGamePacket gamePacket)
        {
            gamePacket = default;

            return false;
        }
    }

    private sealed class EmptyGameNetworkSessionService : IGameNetworkSessionService
    {
        public int Count => 0;

        public void Clear()
        {
        }

        public IReadOnlyCollection<GameSession> GetAll()
            => [];

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotSupportedException();

        public bool Remove(long sessionId)
            => false;

        public bool TryGet(long sessionId, out GameSession session)
        {
            session = null!;

            return false;
        }

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            session = null!;

            return false;
        }
    }

    private sealed class NoOpCharacterService : ICharacterService
    {
        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(false);

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => Task.CompletedTask;

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => Task.FromResult(Serial.Zero);

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult<UOMobileEntity?>(null);

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(false);
    }

    private sealed class NoOpMobileService : IMobileService
    {
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
        ) => Task.FromResult(new List<UOMobileEntity>());

        public Task<UOMobileEntity> SpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(
            new UOMobileEntity
            {
                Id = (Serial)(uint)1,
                MapId = mapId,
                Location = location
            }
        );
    }

    private sealed class NoOpItemService : IItemService
    {
        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => Task.FromResult(Serial.Zero);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromResult(new UOItemEntity());

        public Task<bool> DeleteItemAsync(Serial itemId)
            => Task.FromResult(false);

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        ) => Task.FromResult<DropItemToGroundResult?>(null);

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => Task.FromResult(false);

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((false, (UOItemEntity?)null));

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => Task.FromResult(false);

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(false);

        public Task UpsertItemAsync(UOItemEntity item)
            => Task.CompletedTask;

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;
    }
}
