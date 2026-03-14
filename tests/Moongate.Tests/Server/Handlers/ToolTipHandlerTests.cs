using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Tooltip;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Spans;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.MegaCliloc;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Handlers;

public class ToolTipHandlerTests
{
    private sealed class TestPersistenceService : IPersistenceService
    {
        public TestPersistenceService()
        {
            UnitOfWork = new TestPersistenceUnitOfWork();
        }

        public IPersistenceUnitOfWork UnitOfWork { get; }

        public void Dispose() { }

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return Task.CompletedTask;
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class TestPersistenceUnitOfWork : IPersistenceUnitOfWork
    {
        public TestPersistenceUnitOfWork()
        {
            Accounts = new TestAccountRepository();
            Mobiles = new TestMobileRepository();
            Items = new TestItemRepository();
            BulletinBoardMessages = new TestBulletinBoardMessageRepository();
        }

        public IAccountRepository Accounts { get; }

        public IMobileRepository Mobiles { get; }

        public IItemRepository Items { get; }

        public IBulletinBoardMessageRepository BulletinBoardMessages { get; }

        public Serial AllocateNextAccountId()
            => (Serial)0x00000001u;

        public Serial AllocateNextItemId()
            => (Serial)0x40000001u;

        public Serial AllocateNextMobileId()
            => (Serial)0x00000002u;

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return ValueTask.CompletedTask;
        }

        public ValueTask SaveSnapshotAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestBulletinBoardMessageRepository : IBulletinBoardMessageRepository
    {
        public ValueTask<IReadOnlyCollection<BulletinBoardMessageEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<BulletinBoardMessageEntity>>([]);

        public ValueTask<BulletinBoardMessageEntity?> GetByIdAsync(Serial messageId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<BulletinBoardMessageEntity?>(null);

        public ValueTask<IReadOnlyList<BulletinBoardMessageEntity>> GetByBoardIdAsync(Serial boardId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<BulletinBoardMessageEntity>>([]);

        public ValueTask UpsertAsync(BulletinBoardMessageEntity message, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<bool> RemoveAsync(Serial messageId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(false);
    }

    private sealed class TestItemRepository : IItemRepository
    {
        private readonly Dictionary<Serial, UOItemEntity> _items = [];

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return ValueTask.FromResult(_items.Count);
        }

        public ValueTask<IReadOnlyCollection<UOItemEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return ValueTask.FromResult<IReadOnlyCollection<UOItemEntity>>(_items.Values.ToArray());
        }

        public ValueTask<UOItemEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _items.TryGetValue(id, out var value);

            return ValueTask.FromResult<UOItemEntity?>(value);
        }

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOItemEntity, bool> predicate,
            Func<UOItemEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;

            return ValueTask.FromResult<IReadOnlyList<TResult>>(_items.Values.Where(predicate).Select(selector).ToArray());
        }

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return ValueTask.FromResult(_items.Remove(id));
        }

        public ValueTask UpsertAsync(UOItemEntity item, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _items[item.Id] = item;

            return ValueTask.CompletedTask;
        }

        public ValueTask BulkUpsertAsync(IReadOnlyList<UOItemEntity> items, CancellationToken cancellationToken = default)
        {
            _ = items;
            _ = cancellationToken;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestMobileRepository : IMobileRepository
    {
        private readonly Dictionary<Serial, UOMobileEntity> _mobiles = [];

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return ValueTask.FromResult(_mobiles.Count);
        }

        public ValueTask<IReadOnlyCollection<UOMobileEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return ValueTask.FromResult<IReadOnlyCollection<UOMobileEntity>>(_mobiles.Values.ToArray());
        }

        public ValueTask<UOMobileEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _mobiles.TryGetValue(id, out var value);

            return ValueTask.FromResult<UOMobileEntity?>(value);
        }

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOMobileEntity, bool> predicate,
            Func<UOMobileEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;

            return ValueTask.FromResult<IReadOnlyList<TResult>>(_mobiles.Values.Where(predicate).Select(selector).ToArray());
        }

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return ValueTask.FromResult(_mobiles.Remove(id));
        }

        public ValueTask UpsertAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _mobiles[mobile.Id] = mobile;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestAccountRepository : IAccountRepository
    {
        public ValueTask<bool> AddAsync(UOAccountEntity account, CancellationToken cancellationToken = default)
        {
            _ = account;
            _ = cancellationToken;

            return ValueTask.FromResult(true);
        }

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return ValueTask.FromResult(0);
        }

        public ValueTask<bool> ExistsAsync(
            Func<UOAccountEntity, bool> predicate,
            CancellationToken cancellationToken = default
        )
        {
            _ = predicate;
            _ = cancellationToken;

            return ValueTask.FromResult(false);
        }

        public ValueTask<IReadOnlyCollection<UOAccountEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return ValueTask.FromResult<IReadOnlyCollection<UOAccountEntity>>([]);
        }

        public ValueTask<UOAccountEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = id;
            _ = cancellationToken;

            return ValueTask.FromResult<UOAccountEntity?>(null);
        }

        public ValueTask<UOAccountEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            _ = username;
            _ = cancellationToken;

            return ValueTask.FromResult<UOAccountEntity?>(null);
        }

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOAccountEntity, bool> predicate,
            Func<UOAccountEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
        {
            _ = predicate;
            _ = selector;
            _ = cancellationToken;

            return ValueTask.FromResult<IReadOnlyList<TResult>>([]);
        }

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = id;
            _ = cancellationToken;

            return ValueTask.FromResult(false);
        }

        public ValueTask UpsertAsync(UOAccountEntity account, CancellationToken cancellationToken = default)
        {
            _ = account;
            _ = cancellationToken;

            return ValueTask.CompletedTask;
        }
    }

    [Test]
    public async Task HandlePacketAsync_ShouldEnqueueItemTooltip_WhenItemSerialRequested()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var persistenceService = new TestPersistenceService();
        var itemSerial = (Serial)0x40000010u;
        await persistenceService.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = itemSerial,
                Name = "Gold Coins",
                ItemId = 0x0EED,
                Hue = 0x0021
            }
        );

        var handler = new ToolTipHandler(queue, persistenceService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var request = BuildRequestPacket(itemSerial.Value);

        var handled = await handler.HandlePacketAsync(session, request);
        var dequeued = queue.TryDequeue(out var outbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(dequeued, Is.True);
            }
        );

        var response = DeserializeResponse(outbound.Packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(response.IsClientRequest, Is.False);
                Assert.That(response.Serial, Is.EqualTo(itemSerial));
                Assert.That(response.Properties.Count, Is.GreaterThan(0));
                Assert.That(response.Properties[0].ClilocId, Is.EqualTo(1042971));
                Assert.That(response.Properties[0].Text, Is.EqualTo("Gold Coins"));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldEnqueueMobileTooltip_WhenMobileSerialRequested()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var persistenceService = new TestPersistenceService();
        var mobileSerial = (Serial)0x00000002u;
        await persistenceService.UnitOfWork.Mobiles.UpsertAsync(
            new()
            {
                Id = mobileSerial,
                Name = "Tester",
                Hits = 40,
                MaxHits = 50,
                Mana = 20,
                MaxMana = 30,
                Stamina = 10,
                MaxStamina = 15,
                IsPlayer = true
            }
        );

        var handler = new ToolTipHandler(queue, persistenceService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var request = BuildRequestPacket(mobileSerial.Value);

        var handled = await handler.HandlePacketAsync(session, request);
        var dequeued = queue.TryDequeue(out var outbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(dequeued, Is.True);
            }
        );

        var response = DeserializeResponse(outbound.Packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(response.IsClientRequest, Is.False);
                Assert.That(response.Serial, Is.EqualTo(mobileSerial));
                Assert.That(
                    response.Properties.Any(p => string.Equals(p.Text, "Tester", StringComparison.Ordinal)),
                    Is.True
                );
                Assert.That(
                    response.Properties.Any(p => p.ClilocId == CommonClilocIds.HitPoints && p.Text == "40\t50"),
                    Is.True
                );
                Assert.That(
                    response.Properties.Any(p => p.ClilocId == CommonClilocIds.Mana && p.Text == "20\t30"),
                    Is.True
                );
                Assert.That(
                    response.Properties.Any(p => p.ClilocId == CommonClilocIds.Stamina && p.Text == "10\t15"),
                    Is.True
                );
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldIncludeRarityProperty_WhenItemHasRarity()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var persistenceService = new TestPersistenceService();
        var itemSerial = (Serial)0x40000021u;
        await persistenceService.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = itemSerial,
                ItemId = 0x0EED,
                Hue = 0x0021,
                Rarity = ItemRarity.Legendary
            }
        );

        var handler = new ToolTipHandler(queue, persistenceService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var request = BuildRequestPacket(itemSerial.Value);

        var handled = await handler.HandlePacketAsync(session, request);
        var dequeued = queue.TryDequeue(out var outbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(dequeued, Is.True);
            }
        );

        var response = DeserializeResponse(outbound.Packet);
        var rarityProperty = response.Properties.FirstOrDefault(p => p.ClilocId == CommonClilocIds.ItemRarity);

        Assert.Multiple(
            () =>
            {
                Assert.That(rarityProperty.ClilocId, Is.EqualTo(CommonClilocIds.ItemRarity));
                Assert.That(rarityProperty.Text, Is.EqualTo(ItemRarity.Legendary.ToString()));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldIncludeTypedCombatStatsAndModifiers_WhenItemHasThem()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var persistenceService = new TestPersistenceService();
        var itemSerial = (Serial)0x40000022u;
        await persistenceService.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = itemSerial,
                Name = "Runic Sword",
                ItemId = 0x13B9,
                Weight = 7,
                CombatStats = new()
                {
                    DamageMin = 11,
                    DamageMax = 15,
                    AttackSpeed = 30,
                    MaxDurability = 40,
                    CurrentDurability = 25
                },
                Modifiers = new()
                {
                    PhysicalResist = 10,
                    FireResist = 11,
                    ColdResist = 12,
                    PoisonResist = 13,
                    EnergyResist = 14,
                    HitChanceIncrease = 15,
                    DamageIncrease = 16,
                    SpellDamageIncrease = 17,
                    FasterCasting = 1,
                    FasterCastRecovery = 2,
                    SwingSpeedIncrease = 20,
                    SpellChanneling = 1,
                    UsesRemaining = 25
                }
            }
        );

        var handler = new ToolTipHandler(queue, persistenceService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var request = BuildRequestPacket(itemSerial.Value);

        var handled = await handler.HandlePacketAsync(session, request);
        var dequeued = queue.TryDequeue(out var outbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(dequeued, Is.True);
            }
        );

        var response = DeserializeResponse(outbound.Packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(response.Properties.Any(p => p.ClilocId == CommonClilocIds.WeaponDamage && p.Text == "11\t15"), Is.True);
                Assert.That(response.Properties.Any(p => p.ClilocId == CommonClilocIds.WeaponSpeed && p.Text == "30"), Is.True);
                Assert.That(response.Properties.Any(p => p.ClilocId == CommonClilocIds.Durability && p.Text == "25\t40"), Is.True);
                Assert.That(response.Properties.Any(p => p.ClilocId == CommonClilocIds.PhysicalResist && p.Text == "10"), Is.True);
                Assert.That(response.Properties.Any(p => p.ClilocId == CommonClilocIds.FireResist && p.Text == "11"), Is.True);
                Assert.That(response.Properties.Any(p => p.ClilocId == CommonClilocIds.HitChanceIncrease && p.Text == "15"), Is.True);
                Assert.That(response.Properties.Any(p => p.ClilocId == CommonClilocIds.DamageIncrease && p.Text == "16"), Is.True);
                Assert.That(response.Properties.Any(p => p.ClilocId == CommonClilocIds.SpellDamageIncrease && p.Text == "17"), Is.True);
                Assert.That(response.Properties.Any(p => p.ClilocId == CommonClilocIds.FasterCasting && p.Text == "1"), Is.True);
                Assert.That(response.Properties.Any(p => p.ClilocId == CommonClilocIds.FasterCastRecovery && p.Text == "2"), Is.True);
                Assert.That(response.Properties.Any(p => p.ClilocId == CommonClilocIds.SwingSpeedIncrease && p.Text == "20"), Is.True);
                Assert.That(response.Properties.Any(p => p.ClilocId == CommonClilocIds.SpellChanneling), Is.True);
                Assert.That(response.Properties.Any(p => p.ClilocId == CommonClilocIds.UsesRemaining && p.Text == "25"), Is.True);
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldUseBookMetadata_WhenBookTitleAndAuthorExist()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var persistenceService = new TestPersistenceService();
        var itemSerial = (Serial)0x40000023u;
        var item = new UOItemEntity
        {
            Id = itemSerial,
            Name = "Writable Book",
            ItemId = 0x0FF0
        };
        item.SetCustomString("book_title", "Travel Journal");
        item.SetCustomString("book_author", "Tommy");
        item.SetCustomString("book_content", "Line 1");
        await persistenceService.UnitOfWork.Items.UpsertAsync(item);

        var handler = new ToolTipHandler(queue, persistenceService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var request = BuildRequestPacket(itemSerial.Value);

        var handled = await handler.HandlePacketAsync(session, request);
        var dequeued = queue.TryDequeue(out var outbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(dequeued, Is.True);
            }
        );

        var response = DeserializeResponse(outbound.Packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(response.Properties[0].ClilocId, Is.EqualTo(CommonClilocIds.ObjectName));
                Assert.That(response.Properties[0].Text, Is.EqualTo("Travel Journal"));
                Assert.That(
                    response.Properties.Any(property => string.Equals(property.Text, "by Tommy", StringComparison.Ordinal)),
                    Is.True
                );
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldKeepCacheUsable_AfterSentPacketIsDisposed()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var persistenceService = new TestPersistenceService();
        var itemSerial = (Serial)0x40000033u;
        await persistenceService.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = itemSerial,
                Name = "Dispose Cache Test",
                ItemId = 0x0EED,
                Amount = 10,
                Hue = 0x0021
            }
        );

        var handler = new ToolTipHandler(queue, persistenceService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var request = BuildRequestPacket(itemSerial.Value);

        await handler.HandlePacketAsync(session, request);
        var firstDequeued = queue.TryDequeue(out var firstOutbound);
        Assert.That(firstDequeued, Is.True);

        if (firstOutbound.Packet is IDisposable disposable)
        {
            disposable.Dispose();
        }

        await handler.HandlePacketAsync(session, request);
        var secondDequeued = queue.TryDequeue(out var secondOutbound);
        Assert.That(secondDequeued, Is.True);

        Assert.DoesNotThrow(() => _ = DeserializeResponse(secondOutbound.Packet));
    }

    [Test]
    public async Task HandlePacketAsync_ShouldRebuildItemTooltip_WhenItemHashChanges()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var persistenceService = new TestPersistenceService();
        var itemSerial = (Serial)0x40000032u;
        var item = new UOItemEntity
        {
            Id = itemSerial,
            Name = "Cache Change",
            ItemId = 0x0EED,
            Amount = 10,
            Hue = 0x0021
        };
        await persistenceService.UnitOfWork.Items.UpsertAsync(item);

        var handler = new ToolTipHandler(queue, persistenceService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var request = BuildRequestPacket(itemSerial.Value);

        await handler.HandlePacketAsync(session, request);
        var firstDequeued = queue.TryDequeue(out var firstOutbound);

        item.Amount = 99;
        await persistenceService.UnitOfWork.Items.UpsertAsync(item);

        await handler.HandlePacketAsync(session, request);
        var secondDequeued = queue.TryDequeue(out var secondOutbound);
        Assert.Multiple(
            () =>
            {
                Assert.That(firstDequeued, Is.True);
                Assert.That(secondDequeued, Is.True);
                Assert.That(ReferenceEquals(firstOutbound.Packet, secondOutbound.Packet), Is.False);
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldReuseCachedItemTooltip_WhenItemHashIsUnchanged()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var persistenceService = new TestPersistenceService();
        var itemSerial = (Serial)0x40000031u;
        await persistenceService.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = itemSerial,
                Name = "Cached Item",
                ItemId = 0x0EED,
                Amount = 10,
                Hue = 0x0021
            }
        );

        var handler = new ToolTipHandler(queue, persistenceService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var request = BuildRequestPacket(itemSerial.Value);

        await handler.HandlePacketAsync(session, request);
        var firstDequeued = queue.TryDequeue(out var firstOutbound);
        await handler.HandlePacketAsync(session, request);
        var secondDequeued = queue.TryDequeue(out var secondOutbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(firstDequeued, Is.True);
                Assert.That(secondDequeued, Is.True);
                Assert.That(ReferenceEquals(firstOutbound.Packet, secondOutbound.Packet), Is.False);
            }
        );
    }

    private static MegaClilocPacket BuildRequestPacket(params uint[] serials)
    {
        var writer = new SpanWriter(64, true);

        try
        {
            writer.Write((byte)0xD6);
            writer.Write((ushort)(3 + serials.Length * 4));

            foreach (var serial in serials)
            {
                writer.Write(serial);
            }

            var requestBytes = writer.ToArray();
            var packet = new MegaClilocPacket();
            var parsed = packet.TryParse(requestBytes);

            Assert.That(parsed, Is.True);

            return packet;
        }
        finally
        {
            writer.Dispose();
        }
    }

    private static MegaClilocPacket DeserializeResponse(IGameNetworkPacket packet)
    {
        var writer = new SpanWriter(256, true);

        try
        {
            packet.Write(ref writer);
            var bytes = writer.ToArray();
            var response = new MegaClilocPacket();
            var parsed = response.TryParse(bytes);

            Assert.That(parsed, Is.True);

            return response;
        }
        finally
        {
            writer.Dispose();
        }
    }
}
