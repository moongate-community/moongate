using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Data.BulletinBoard;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Services.Interaction;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class BulletinBoardServiceTests
{
    private readonly List<MoongateTCPClient> _clientsToDispose = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var client in _clientsToDispose)
        {
            client.Dispose();
        }
    }

    [Test]
    public async Task OpenBoardAsync_ShouldEnqueueDisplayAndSummaries()
    {
        var service = CreateService(out var itemService, out var queue, out var sessionService, out var persistenceService, out _);
        var session = AddSession(sessionService, (Serial)0x00000055u);
        var board = CreateBoard();
        itemService.Items[board.Id] = board;

        var topLevel = CreateMessage((Serial)0x40000091u, board.Id, Serial.Zero, session.CharacterId, "Poster", "Subject A", ["Body A"]);
        var reply = CreateMessage((Serial)0x40000092u, board.Id, topLevel.MessageId, session.CharacterId, "Poster", "Subject B", ["Body B"]);
        await persistenceService.UnitOfWork.BulletinBoardMessages.UpsertAsync(topLevel);
        await persistenceService.UnitOfWork.BulletinBoardMessages.UpsertAsync(reply);

        var opened = await service.OpenBoardAsync(session.SessionId, board.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(opened, Is.True);
                Assert.That(queue.CurrentQueueDepth, Is.EqualTo(3));
            }
        );

        Assert.That(queue.TryDequeue(out var display), Is.True);
        Assert.That(display.Packet, Is.TypeOf<BulletinBoardDisplayPacket>());
        Assert.That(queue.TryDequeue(out var summaryA), Is.True);
        Assert.That(summaryA.Packet, Is.TypeOf<BulletinBoardSummaryPacket>());
        Assert.That(queue.TryDequeue(out var summaryB), Is.True);
        Assert.That(summaryB.Packet, Is.TypeOf<BulletinBoardSummaryPacket>());
    }

    [Test]
    public async Task SendMessageAsync_ShouldEnqueueFullMessagePacket()
    {
        var service = CreateService(out var itemService, out var queue, out var sessionService, out var persistenceService, out _);
        var session = AddSession(sessionService, (Serial)0x00000055u);
        var board = CreateBoard();
        itemService.Items[board.Id] = board;
        var message = CreateMessage((Serial)0x40000091u, board.Id, Serial.Zero, session.CharacterId, "Poster", "Subject A", ["Body A"]);
        await persistenceService.UnitOfWork.BulletinBoardMessages.UpsertAsync(message);

        var ok = await service.SendMessageAsync(session, (uint)board.Id, (uint)message.MessageId);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(queue.TryDequeue(out var outgoing), Is.True);
                Assert.That(outgoing.Packet, Is.TypeOf<BulletinBoardMessagePacket>());
            }
        );
    }

    [Test]
    public async Task PostMessageAsync_ShouldPersistAndEnqueueSummary()
    {
        var service = CreateService(out var itemService, out var queue, out var sessionService, out var persistenceService, out var characterService);
        var session = AddSession(sessionService, (Serial)0x00000055u);
        characterService.Character = new UOMobileEntity { Id = session.CharacterId, Name = "Poster" };
        var board = CreateBoard();
        itemService.Items[board.Id] = board;

        var packet = BuildPostPacket((uint)board.Id, 0u, "Subject", ["Line One", "Line Two"]);

        var ok = await service.PostMessageAsync(session, packet);
        var messages = await persistenceService.UnitOfWork.BulletinBoardMessages.GetByBoardIdAsync(board.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(messages.Count, Is.EqualTo(1));
                Assert.That(messages[0].Subject, Is.EqualTo("Subject"));
                Assert.That(messages[0].Author, Is.EqualTo("Poster"));
                Assert.That(queue.TryDequeue(out var outgoing), Is.True);
                Assert.That(outgoing.Packet, Is.TypeOf<BulletinBoardSummaryPacket>());
            }
        );
    }

    [Test]
    public async Task RemoveMessageAsync_WhenOwnerAndLeaf_ShouldDelete()
    {
        var service = CreateService(out var itemService, out _, out var sessionService, out var persistenceService, out _);
        var session = AddSession(sessionService, (Serial)0x00000055u);
        var board = CreateBoard();
        itemService.Items[board.Id] = board;
        var message = CreateMessage((Serial)0x40000091u, board.Id, Serial.Zero, session.CharacterId, "Poster", "Subject A", ["Body A"]);
        await persistenceService.UnitOfWork.BulletinBoardMessages.UpsertAsync(message);

        var ok = await service.RemoveMessageAsync(session, (uint)board.Id, (uint)message.MessageId);
        var restored = await persistenceService.UnitOfWork.BulletinBoardMessages.GetByIdAsync(message.MessageId);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(restored, Is.Null);
            }
        );
    }

    private BulletinBoardMessagesPacket BuildPostPacket(uint boardId, uint parentId, string subject, IReadOnlyList<string> bodyLines)
    {
        var writer = new Moongate.Network.Spans.SpanWriter(256, true);
        writer.Write((byte)0x71);
        writer.Write((ushort)0);
        writer.Write((byte)BulletinBoardSubcommand.PostMessage);
        writer.Write(boardId);
        writer.Write(parentId);
        WriteAscii(ref writer, subject);
        writer.Write((byte)bodyLines.Count);

        foreach (var line in bodyLines)
        {
            WriteAscii(ref writer, line);
        }

        writer.WritePacketLength();
        var bytes = writer.ToArray();
        writer.Dispose();

        var packet = new BulletinBoardMessagesPacket();
        Assert.That(packet.TryParse(bytes), Is.True);

        return packet;
    }

    private static void WriteAscii(ref Moongate.Network.Spans.SpanWriter writer, string value)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        writer.Write((byte)(bytes.Length + 1));
        writer.Write(bytes);
        writer.Write((byte)0);
    }

    private static BulletinBoardMessageEntity CreateMessage(Serial messageId, Serial boardId, Serial parentId, Serial ownerId, string author, string subject, IReadOnlyList<string> bodyLines)
    {
        var message = new BulletinBoardMessageEntity
        {
            MessageId = messageId,
            BoardId = boardId,
            ParentId = parentId,
            OwnerCharacterId = ownerId,
            Author = author,
            Subject = subject,
            PostedAtUtc = new DateTime(2026, 3, 13, 12, 0, 0, DateTimeKind.Utc)
        };
        message.BodyLines.AddRange(bodyLines);

        return message;
    }

    private static UOItemEntity CreateBoard()
        => new()
        {
            Id = (Serial)0x40000055u,
            ItemId = 0x1E5E,
            Name = "Town Board",
            ScriptId = "items.bulletin_board",
            Location = new Point3D(100, 100, 0),
            MapId = 0
        };

    private GameSession AddSession(FakeGameNetworkSessionService sessionService, Serial characterId)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var client = new MoongateTCPClient(socket);
        _clientsToDispose.Add(client);
        var session = new GameSession(new GameNetworkSession(client))
        {
            CharacterId = characterId
        };
        sessionService.Add(session);

        return session;
    }

    private BulletinBoardService CreateService(
        out BulletinBoardTestItemService itemService,
        out BasePacketListenerTestOutgoingPacketQueue queue,
        out FakeGameNetworkSessionService sessionService,
        out BulletinBoardTestPersistenceService persistenceService,
        out BulletinBoardTestCharacterService characterService
    )
    {
        itemService = new BulletinBoardTestItemService();
        queue = new BasePacketListenerTestOutgoingPacketQueue();
        sessionService = new FakeGameNetworkSessionService();
        persistenceService = new BulletinBoardTestPersistenceService();
        characterService = new BulletinBoardTestCharacterService();

        return new BulletinBoardService(itemService, characterService, sessionService, queue, persistenceService);
    }

    private sealed class BulletinBoardTestPersistenceService : IPersistenceService
    {
        public BulletinBoardTestPersistenceService()
        {
            UnitOfWork = new BulletinBoardTestPersistenceUnitOfWork();
        }

        public IPersistenceUnitOfWork UnitOfWork { get; }

        public void Dispose()
        { }

        public Task SaveAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class BulletinBoardTestPersistenceUnitOfWork : IPersistenceUnitOfWork
    {
        public BulletinBoardTestPersistenceUnitOfWork()
        {
            Accounts = new BulletinBoardUnusedAccountRepository();
            Mobiles = new BulletinBoardUnusedMobileRepository();
            Items = new BulletinBoardUnusedItemRepository();
            BulletinBoardMessages = new BulletinBoardInMemoryMessageRepository();
        }

        public IAccountRepository Accounts { get; }
        public IMobileRepository Mobiles { get; }
        public IItemRepository Items { get; }
        public IBulletinBoardMessageRepository BulletinBoardMessages { get; }
        private uint _nextItemId = Serial.ItemOffset;

        public Serial AllocateNextAccountId() => (Serial)1u;
        public Serial AllocateNextItemId() => (Serial)_nextItemId++;
        public Serial AllocateNextMobileId() => (Serial)1u;
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SaveSnapshotAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class BulletinBoardInMemoryMessageRepository : IBulletinBoardMessageRepository
    {
        private readonly Dictionary<Serial, BulletinBoardMessageEntity> _messages = [];

        public ValueTask<IReadOnlyCollection<BulletinBoardMessageEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<BulletinBoardMessageEntity>>([.. _messages.Values]);

        public ValueTask<BulletinBoardMessageEntity?> GetByIdAsync(Serial messageId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_messages.TryGetValue(messageId, out var message) ? Clone(message) : null);

        public ValueTask<IReadOnlyList<BulletinBoardMessageEntity>> GetByBoardIdAsync(Serial boardId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<BulletinBoardMessageEntity>>([.. _messages.Values.Where(m => m.BoardId == boardId).OrderBy(m => m.PostedAtUtc).Select(Clone)]);

        public ValueTask UpsertAsync(BulletinBoardMessageEntity message, CancellationToken cancellationToken = default)
        {
            _messages[message.MessageId] = Clone(message);

            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> RemoveAsync(Serial messageId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_messages.Remove(messageId));

        private static BulletinBoardMessageEntity Clone(BulletinBoardMessageEntity message)
        {
            var clone = new BulletinBoardMessageEntity
            {
                MessageId = message.MessageId,
                BoardId = message.BoardId,
                ParentId = message.ParentId,
                OwnerCharacterId = message.OwnerCharacterId,
                Author = message.Author,
                Subject = message.Subject,
                PostedAtUtc = message.PostedAtUtc
            };
            clone.BodyLines.AddRange(message.BodyLines);

            return clone;
        }
    }

    private sealed class BulletinBoardUnusedAccountRepository : IAccountRepository
    {
        public ValueTask<bool> AddAsync(UOAccountEntity account, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(0);
        public ValueTask<bool> ExistsAsync(Func<UOAccountEntity, bool> predicate, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<IReadOnlyCollection<UOAccountEntity>> GetAllAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyCollection<UOAccountEntity>>([]);
        public ValueTask<UOAccountEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default) => ValueTask.FromResult<UOAccountEntity?>(null);
        public ValueTask<UOAccountEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => ValueTask.FromResult<UOAccountEntity?>(null);
        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(Func<UOAccountEntity, bool> predicate, Func<UOAccountEntity, TResult> selector, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<TResult>>([]);
        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask UpsertAsync(UOAccountEntity account, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class BulletinBoardUnusedMobileRepository : IMobileRepository
    {
        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(0);
        public ValueTask<IReadOnlyCollection<UOMobileEntity>> GetAllAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyCollection<UOMobileEntity>>([]);
        public ValueTask<UOMobileEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default) => ValueTask.FromResult<UOMobileEntity?>(null);
        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(Func<UOMobileEntity, bool> predicate, Func<UOMobileEntity, TResult> selector, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<TResult>>([]);
        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask UpsertAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class BulletinBoardUnusedItemRepository : IItemRepository
    {
        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(0);
        public ValueTask<IReadOnlyCollection<UOItemEntity>> GetAllAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyCollection<UOItemEntity>>([]);
        public ValueTask<UOItemEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default) => ValueTask.FromResult<UOItemEntity?>(null);
        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(Func<UOItemEntity, bool> predicate, Func<UOItemEntity, TResult> selector, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<TResult>>([]);
        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask UpsertAsync(UOItemEntity item, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask BulkUpsertAsync(IReadOnlyList<UOItemEntity> items, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class BulletinBoardTestItemService : IItemService
    {
        public Dictionary<Serial, UOItemEntity> Items { get; } = [];

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true) => item;
        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true) => Task.FromResult<UOItemEntity?>(null);
        public Task<Serial> CreateItemAsync(UOItemEntity item) => Task.FromResult(item.Id);
        public Task<bool> DeleteItemAsync(Serial itemId) => Task.FromResult(false);
        public Task<Moongate.Server.Data.Items.DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0) => Task.FromResult<Moongate.Server.Data.Items.DropItemToGroundResult?>(null);
        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer) => Task.FromResult(false);
        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY) => Task.FromResult(new List<UOItemEntity>());
        public Task<UOItemEntity?> GetItemAsync(Serial itemId) => Task.FromResult(Items.TryGetValue(itemId, out var item) ? item : null);
        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId) => Task.FromResult(new List<UOItemEntity>());
        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0) => Task.FromResult(false);
        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0) => Task.FromResult(false);
        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId) => Task.FromResult(new UOItemEntity());
        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId) => Task.FromResult(Items.TryGetValue(itemId, out var item) ? (true, item) : (false, (UOItemEntity?)null));
        public Task UpsertItemAsync(UOItemEntity item) => Task.CompletedTask;
        public Task UpsertItemsAsync(params UOItemEntity[] items) => Task.CompletedTask;
        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items) => Task.CompletedTask;
    }

    private sealed class BulletinBoardTestCharacterService : ICharacterService
    {
        public UOMobileEntity? Character { get; set; }
        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId) => Task.FromResult(false);
        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue) => Task.CompletedTask;
        public Task<Serial> CreateCharacterAsync(UOMobileEntity character) => Task.FromResult(character.Id);
        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character) => Task.FromResult<UOItemEntity?>(null);
        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character) => Task.FromResult<UOItemEntity?>(null);
        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId) => Task.FromResult(Character?.Id == characterId ? Character : null);
        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId) => Task.FromResult(new List<UOMobileEntity>());
        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId) => Task.FromResult(false);
    }
}
