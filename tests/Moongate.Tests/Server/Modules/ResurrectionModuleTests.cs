using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Modules;
using Moongate.Server.Types.Interaction;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Modules;

public sealed class ResurrectionModuleTests
{
    [Test]
    public void OfferAnkh_WhenGhostPlayerIsInRange_ShouldCreateOfferWithAnkhContext()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var player = new UOMobileEntity
        {
            Id = (Serial)0x00004001u,
            IsPlayer = true,
            IsAlive = false,
            MapId = 0,
            Location = new Point3D(100, 100, 0)
        };
        var ankh = new UOItemEntity
        {
            Id = (Serial)0x40004001u,
            MapId = 0,
            Location = new Point3D(101, 100, 0)
        };
        ankh.SetCustomString(ItemCustomParamKeys.Interaction.ResurrectionSource, "ankh");
        var session = new GameSession(new(client))
        {
            Character = player,
            CharacterId = player.Id
        };
        var sessionService = new TestGameNetworkSessionService();
        sessionService.Add(session);
        var itemService = new RecordingItemService();
        itemService.Items[ankh.Id] = ankh;
        var offerService = new RecordingResurrectionOfferService();
        var module = new ResurrectionModule(offerService, sessionService, itemService);

        var result = module.OfferAnkh(session.SessionId, (uint)player.Id, (uint)ankh.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(offerService.CallCount, Is.EqualTo(1));
                Assert.That(offerService.LastSessionId, Is.EqualTo(session.SessionId));
                Assert.That(offerService.LastCharacterId, Is.EqualTo(player.Id));
                Assert.That(offerService.LastSourceType, Is.EqualTo(ResurrectionOfferSourceType.Ankh));
                Assert.That(offerService.LastSourceSerial, Is.EqualTo(ankh.Id));
                Assert.That(offerService.LastMapId, Is.EqualTo(ankh.MapId));
                Assert.That(offerService.LastSourceLocation, Is.EqualTo(ankh.Location));
            }
        );
    }

    [Test]
    public void OfferAnkh_WhenItemIsNotResurrectionSource_ShouldReturnFalse()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var player = new UOMobileEntity
        {
            Id = (Serial)0x00004002u,
            IsPlayer = true,
            IsAlive = false,
            MapId = 0,
            Location = new Point3D(100, 100, 0)
        };
        var nonAnkh = new UOItemEntity
        {
            Id = (Serial)0x40004002u,
            MapId = 0,
            Location = new Point3D(101, 100, 0)
        };
        var session = new GameSession(new(client))
        {
            Character = player,
            CharacterId = player.Id
        };
        var sessionService = new TestGameNetworkSessionService();
        sessionService.Add(session);
        var itemService = new RecordingItemService();
        itemService.Items[nonAnkh.Id] = nonAnkh;
        var offerService = new RecordingResurrectionOfferService();
        var module = new ResurrectionModule(offerService, sessionService, itemService);

        var result = module.OfferAnkh(session.SessionId, (uint)player.Id, (uint)nonAnkh.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.False);
                Assert.That(offerService.CallCount, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public void Accept_ShouldDelegateToOfferService()
    {
        var offerService = new RecordingResurrectionOfferService
        {
            AcceptResult = true
        };
        var module = new ResurrectionModule(
            offerService,
            new TestGameNetworkSessionService(),
            new RecordingItemService()
        );

        var result = module.Accept(42);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(offerService.AcceptCalls, Is.EqualTo(1));
                Assert.That(offerService.LastAcceptedSessionId, Is.EqualTo(42));
            }
        );
    }

    [Test]
    public void Decline_ShouldDelegateToOfferService()
    {
        var offerService = new RecordingResurrectionOfferService();
        var module = new ResurrectionModule(
            offerService,
            new TestGameNetworkSessionService(),
            new RecordingItemService()
        );

        var result = module.Decline(77);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(offerService.DeclinedSessionIds, Is.EqualTo(new[] { 77L }));
            }
        );
    }

    private sealed class RecordingResurrectionOfferService : IResurrectionOfferService
    {
        public bool AcceptResult { get; set; }

        public int AcceptCalls { get; private set; }

        public int CallCount { get; private set; }

        public List<long> DeclinedSessionIds { get; } = [];

        public long LastAcceptedSessionId { get; private set; }

        public Serial LastCharacterId { get; private set; }

        public int LastMapId { get; private set; }

        public long LastSessionId { get; private set; }

        public Point3D LastSourceLocation { get; private set; }

        public Serial LastSourceSerial { get; private set; }

        public ResurrectionOfferSourceType LastSourceType { get; private set; }

        public void Decline(long sessionId)
            => DeclinedSessionIds.Add(sessionId);

        public Task<bool> TryAcceptAsync(long sessionId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            AcceptCalls++;
            LastAcceptedSessionId = sessionId;

            return Task.FromResult(AcceptResult);
        }

        public Task<bool> TryCreateOfferAsync(
            long sessionId,
            Serial characterId,
            ResurrectionOfferSourceType sourceType,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            CallCount++;
            LastSessionId = sessionId;
            LastCharacterId = characterId;
            LastSourceType = sourceType;

            return Task.FromResult(true);
        }

        public Task<bool> TryCreateOfferAsync(
            long sessionId,
            Serial characterId,
            ResurrectionOfferSourceType sourceType,
            Serial sourceSerial,
            int mapId,
            Point3D sourceLocation,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            CallCount++;
            LastSessionId = sessionId;
            LastCharacterId = characterId;
            LastSourceType = sourceType;
            LastSourceSerial = sourceSerial;
            LastMapId = mapId;
            LastSourceLocation = sourceLocation;

            return Task.FromResult(true);
        }
    }

    private sealed class RecordingItemService : IItemService
    {
        public Dictionary<Serial, UOItemEntity> Items { get; } = [];

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => throw new NotSupportedException();

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task<bool> DeleteItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<Moongate.Server.Data.Items.DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, Moongate.UO.Data.Types.ItemLayerType layer)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(Items.GetValueOrDefault(itemId));

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
        {
            var found = Items.TryGetValue(itemId, out var item);

            return Task.FromResult((found, item));
        }

        public Task UpsertItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => throw new NotSupportedException();
    }

    private sealed class TestGameNetworkSessionService : IGameNetworkSessionService
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
        {
            var removed = _sessions.RemoveAll(session => session.SessionId == sessionId);

            return removed > 0;
        }

        public bool TryGet(long sessionId, out GameSession session)
        {
            session = _sessions.FirstOrDefault(candidate => candidate.SessionId == sessionId)!;

            return session is not null;
        }

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            session = _sessions.FirstOrDefault(candidate => candidate.CharacterId == characterId)!;

            return session is not null;
        }
    }
}
