using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Trading;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.Trading;
using Moongate.Network.Spans;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Services.Interaction;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class PlayerSellBuyServiceTests
{
    private sealed class TestSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public int Count => _sessions.Count;

        public void Add(GameSession session)
            => _sessions[session.SessionId] = session;

        public void Clear()
            => _sessions.Clear();

        public IReadOnlyCollection<GameSession> GetAll()
            => [.. _sessions.Values];

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotSupportedException();

        public bool Remove(long sessionId)
            => _sessions.Remove(sessionId);

        public bool TryGet(long sessionId, out GameSession session)
            => _sessions.TryGetValue(sessionId, out session!);

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            session = _sessions.Values.FirstOrDefault(s => s.CharacterId == characterId)!;

            return session is not null;
        }
    }

    private sealed class TestMobileService : IMobileService
    {
        public Dictionary<Serial, UOMobileEntity> Mobiles { get; } = [];

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(Mobiles.GetValueOrDefault(id));

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
            int mapId,
            int sectorX,
            int sectorY,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<UOMobileEntity> SpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();
    }

    private sealed class TestCharacterService : ICharacterService
    {
        public UOItemEntity? Backpack { get; set; }
        public UOItemEntity? BankBox { get; set; }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => throw new NotSupportedException();

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => throw new NotSupportedException();

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => Task.FromResult(Backpack);

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
            => Task.FromResult(BankBox);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => throw new NotSupportedException();

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => throw new NotSupportedException();

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => throw new NotSupportedException();
    }

    private sealed class TestItemFactoryService : IItemFactoryService
    {
        public Dictionary<string, ItemTemplateDefinition> Templates { get; } = new(StringComparer.OrdinalIgnoreCase);

        public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
        {
            if (!Templates.TryGetValue(itemTemplateId, out var template))
            {
                throw new InvalidOperationException($"Missing template '{itemTemplateId}'.");
            }

            return new()
            {
                Id = Serial.RandomSerial() >= Serial.ItemOffsetSerial
                         ? Serial.RandomSerial()
                         : (Serial)(Serial.ItemOffset + 1u),
                ItemId = ParseItemId(template.ItemId),
                Hue = 0,
                Name = template.Name,
                Amount = 1,
                IsStackable = true
            };
        }

        public UOItemEntity GetNewBackpack()
            => throw new NotSupportedException();

        public bool TryGetItemTemplate(string itemTemplateId, out ItemTemplateDefinition? definition)
            => Templates.TryGetValue(itemTemplateId, out definition);

        private static int ParseItemId(string value)
            => value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                   ? Convert.ToInt32(value[2..], 16)
                   : int.Parse(value);
    }

    private sealed class TestItemService : IItemService
    {
        public Dictionary<Serial, UOItemEntity> CreatedItems { get; } = [];
        public HashSet<Serial> DeletedItems { get; } = [];
        public HashSet<Serial> UpsertedItems { get; } = [];

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
        {
            CreatedItems[item.Id] = item;

            return Task.FromResult(item.Id);
        }

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            DeletedItems.Add(itemId);
            _ = CreatedItems.Remove(itemId);

            return Task.FromResult(true);
        }

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(CreatedItems.GetValueOrDefault(itemId));

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
        {
            if (!CreatedItems.TryGetValue(itemId, out var item))
            {
                return Task.FromResult(false);
            }

            item.ParentContainerId = containerId;
            item.ContainerPosition = position;

            return Task.FromResult(true);
        }

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((CreatedItems.TryGetValue(itemId, out var item), item));

        public Task UpsertItemAsync(UOItemEntity item)
        {
            CreatedItems[item.Id] = item;
            UpsertedItems.Add(item.Id);

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;
    }

    [Test]
    public async Task HandleBuyItemsAsync_ShouldConsumeGoldAndCreateBoughtItemInBackpack()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var sessions = new TestSessionService();
        var mobiles = new TestMobileService();
        var characters = new TestCharacterService();
        var itemFactory = new TestItemFactoryService();
        var itemService = new TestItemService();
        var sellProfiles = new SellProfileTemplateService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();

        var backpack = new UOItemEntity { Id = (Serial)0x40000101u, ItemId = 0x0E75 };
        var gold = new UOItemEntity { Id = (Serial)0x40000102u, ItemId = 0x0EED, Amount = 100, IsStackable = true };
        backpack.AddItem(gold, new(1, 1));
        var character = new UOMobileEntity
            { Id = (Serial)0x00000101u, Location = new(100, 100, 0), MapId = 0, BackpackId = backpack.Id };
        character.AddEquippedItem(ItemLayerType.Backpack, backpack);
        characters.Backpack = backpack;

        var session = new GameSession(new(client)) { Character = character, CharacterId = character.Id };
        sessions.Add(session);

        var vendor = new UOMobileEntity { Id = (Serial)0x00000102u, Location = new(101, 100, 0), MapId = 0 };
        vendor.SetCustomString("sell_profile_id", "vendor.weaponsmith");
        mobiles.Mobiles[vendor.Id] = vendor;

        itemFactory.Templates["dagger"] = new() { Id = "dagger", Name = "Dagger", ItemId = "0x0F51" };
        sellProfiles.Upsert(
            new()
            {
                Id = "vendor.weaponsmith", Name = "Weaponsmith",
                VendorItems = [new() { ItemTemplateId = "dagger", Price = 12, MaxStock = 20 }]
            }
        );

        var service = new PlayerSellBuyService(
            sessions,
            mobiles,
            characters,
            itemFactory,
            itemService,
            sellProfiles,
            outgoing
        );

        await service.HandleVendorBuyRequestAsync(session.SessionId, vendor.Id);
        var displaySerial = DrainPackets(outgoing)
                            .OfType<AddMultipleItemsToContainerPacket>()
                            .Single()
                            .Container!
                            .Items
                            .Single()
                            .Id;

        var buyReply = ParseBuyItemsPacket(vendor.Id, displaySerial, 2);

        await service.HandleBuyItemsAsync(session.SessionId, buyReply);

        var outboundPackets = DrainPackets(outgoing);

        Assert.Multiple(
            () =>
            {
                Assert.That(gold.Amount, Is.EqualTo(76));
                Assert.That(itemService.CreatedItems.Values.Any(i => i.ItemId == 0x0F51 && i.Amount == 2), Is.True);
                Assert.That(itemService.UpsertedItems, Does.Contain(gold.Id));
                Assert.That(outboundPackets.Any(packet => packet is DrawContainerAndAddItemCombinedPacket), Is.True);
            }
        );
    }

    [Test]
    public async Task HandleBuyItemsAsync_ShouldDeleteConsumedGoldStackWhenSpentCompletely()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var sessions = new TestSessionService();
        var mobiles = new TestMobileService();
        var characters = new TestCharacterService();
        var itemFactory = new TestItemFactoryService();
        var itemService = new TestItemService();
        var sellProfiles = new SellProfileTemplateService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();

        var backpack = new UOItemEntity { Id = (Serial)0x40000121u, ItemId = 0x0E75 };
        var gold = new UOItemEntity { Id = (Serial)0x40000122u, ItemId = 0x0EED, Amount = 24, IsStackable = true };
        backpack.AddItem(gold, new(1, 1));
        var character = new UOMobileEntity
            { Id = (Serial)0x00000121u, Location = new(100, 100, 0), MapId = 0, BackpackId = backpack.Id };
        character.AddEquippedItem(ItemLayerType.Backpack, backpack);
        characters.Backpack = backpack;

        var session = new GameSession(new(client)) { Character = character, CharacterId = character.Id };
        sessions.Add(session);

        var vendor = new UOMobileEntity { Id = (Serial)0x00000122u, Location = new(101, 100, 0), MapId = 0 };
        vendor.SetCustomString("sell_profile_id", "vendor.weaponsmith");
        mobiles.Mobiles[vendor.Id] = vendor;

        itemFactory.Templates["dagger"] = new() { Id = "dagger", Name = "Dagger", ItemId = "0x0F51" };
        sellProfiles.Upsert(
            new()
            {
                Id = "vendor.weaponsmith", Name = "Weaponsmith",
                VendorItems = [new() { ItemTemplateId = "dagger", Price = 12, MaxStock = 20 }]
            }
        );

        var service = new PlayerSellBuyService(
            sessions,
            mobiles,
            characters,
            itemFactory,
            itemService,
            sellProfiles,
            outgoing
        );

        await service.HandleVendorBuyRequestAsync(session.SessionId, vendor.Id);
        var displaySerial = DrainPackets(outgoing)
                            .OfType<AddMultipleItemsToContainerPacket>()
                            .Single()
                            .Container!
                            .Items
                            .Single()
                            .Id;
        var buyReply = ParseBuyItemsPacket(vendor.Id, displaySerial, 2);

        await service.HandleBuyItemsAsync(session.SessionId, buyReply);

        Assert.Multiple(
            () =>
            {
                Assert.That(backpack.Items.Any(i => i.Id == gold.Id), Is.False);
                Assert.That(itemService.DeletedItems, Does.Contain(gold.Id));
            }
        );
    }

    [Test]
    public async Task HandleSellListReplyAsync_ShouldRemoveSoldItemAndCreditGold()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var sessions = new TestSessionService();
        var mobiles = new TestMobileService();
        var characters = new TestCharacterService();
        var itemFactory = new TestItemFactoryService();
        var itemService = new TestItemService();
        var sellProfiles = new SellProfileTemplateService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();

        var backpack = new UOItemEntity { Id = (Serial)0x40000111u, ItemId = 0x0E75 };
        var gold = new UOItemEntity { Id = (Serial)0x40000112u, ItemId = 0x0EED, Amount = 10, IsStackable = true };
        var soldItem = new UOItemEntity { Id = (Serial)0x40000113u, ItemId = 0x0F51, Amount = 3, IsStackable = true };
        backpack.AddItem(gold, new(1, 1));
        backpack.AddItem(soldItem, new(2, 1));

        var character = new UOMobileEntity
            { Id = (Serial)0x00000111u, Location = new(100, 100, 0), MapId = 0, BackpackId = backpack.Id };
        character.AddEquippedItem(ItemLayerType.Backpack, backpack);
        characters.Backpack = backpack;

        var session = new GameSession(new(client)) { Character = character, CharacterId = character.Id };
        sessions.Add(session);

        var vendor = new UOMobileEntity { Id = (Serial)0x00000112u, Location = new(101, 100, 0), MapId = 0 };
        vendor.SetCustomString("sell_profile_id", "vendor.weaponsmith");
        mobiles.Mobiles[vendor.Id] = vendor;

        itemFactory.Templates["dagger"] = new() { Id = "dagger", Name = "Dagger", ItemId = "0x0F51" };
        itemFactory.Templates["gold"] = new() { Id = "gold", Name = "Gold", ItemId = "0x0EED" };
        sellProfiles.Upsert(
            new()
            {
                Id = "vendor.weaponsmith", Name = "Weaponsmith",
                AcceptedItems = [new() { ItemTemplateId = "dagger", Price = 6 }]
            }
        );

        var service = new PlayerSellBuyService(
            sessions,
            mobiles,
            characters,
            itemFactory,
            itemService,
            sellProfiles,
            outgoing
        );

        await service.HandleVendorSellRequestAsync(session.SessionId, vendor.Id);
        _ = DrainPackets(outgoing);
        var reply = ParseSellListReplyPacket(vendor.Id, soldItem.Id, 2);

        await service.HandleSellListReplyAsync(session.SessionId, reply);

        var outboundPackets = DrainPackets(outgoing);

        Assert.Multiple(
            () =>
            {
                Assert.That(soldItem.Amount, Is.EqualTo(1));
                Assert.That(gold.Amount, Is.EqualTo(22));
                Assert.That(outboundPackets.Any(packet => packet is DrawContainerAndAddItemCombinedPacket), Is.True);
            }
        );
    }

    [Test]
    public async Task HandleVendorBuyRequestAsync_ShouldEnqueueClassicVendorBuyFlow()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var sessions = new TestSessionService();
        var mobiles = new TestMobileService();
        var characters = new TestCharacterService();
        var itemFactory = new TestItemFactoryService();
        var itemService = new TestItemService();
        var sellProfiles = new SellProfileTemplateService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();

        var backpack = new UOItemEntity { Id = (Serial)0x40000081u, ItemId = 0x0E75 };
        var character = new UOMobileEntity
        {
            Id = (Serial)0x00000042u,
            Name = "buyer",
            Location = new(100, 100, 0),
            MapId = 0,
            BackpackId = backpack.Id
        };
        character.AddEquippedItem(ItemLayerType.Backpack, backpack);
        backpack.AddItem(
            new UOItemEntity { Id = (Serial)0x40000082u, ItemId = 0x0EED, Amount = 100, IsStackable = true },
            new(1, 1)
        );
        characters.Backpack = backpack;

        var session = new GameSession(new(client))
        {
            Character = character,
            CharacterId = character.Id
        };
        sessions.Add(session);

        var vendor = new UOMobileEntity
        {
            Id = (Serial)0x00000077u,
            Name = "smith",
            Location = new(101, 100, 0),
            MapId = 0
        };
        vendor.SetCustomString("sell_profile_id", "vendor.weaponsmith");
        mobiles.Mobiles[vendor.Id] = vendor;

        itemFactory.Templates["dagger"] = new() { Id = "dagger", Name = "Dagger", ItemId = "0x0F51" };
        sellProfiles.Upsert(
            new()
            {
                Id = "vendor.weaponsmith",
                Name = "Weaponsmith",
                VendorItems = [new() { ItemTemplateId = "dagger", Price = 12, MaxStock = 20 }]
            }
        );

        var service = new PlayerSellBuyService(
            sessions,
            mobiles,
            characters,
            itemFactory,
            itemService,
            sellProfiles,
            outgoing
        );

        await service.HandleVendorBuyRequestAsync(session.SessionId, vendor.Id);

        var packetTypes = DrainPacketTypes(outgoing);

        Assert.Multiple(
            () =>
            {
                Assert.That(packetTypes, Does.Contain(typeof(WornItemPacket)));
                Assert.That(packetTypes, Does.Contain(typeof(AddMultipleItemsToContainerPacket)));
                Assert.That(packetTypes, Does.Contain(typeof(VendorBuyListPacket)));
                Assert.That(packetTypes, Does.Contain(typeof(DrawContainerPacket)));
                Assert.That(packetTypes, Does.Contain(typeof(PlayerStatusPacket)));
            }
        );
    }

    [Test]
    public async Task HandleVendorBuyRequestAsync_ShouldHydrateGoldBeforeSendingPlayerStatus()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var sessions = new TestSessionService();
        var mobiles = new TestMobileService();
        var characters = new TestCharacterService();
        var itemFactory = new TestItemFactoryService();
        var itemService = new TestItemService();
        var sellProfiles = new SellProfileTemplateService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();

        var backpack = new UOItemEntity { Id = (Serial)0x400000A1u, ItemId = 0x0E75 };
        backpack.AddItem(
            new UOItemEntity { Id = (Serial)0x400000A2u, ItemId = 0x0EED, Amount = 750, IsStackable = true },
            new(1, 1)
        );
        var bank = new UOItemEntity { Id = (Serial)0x400000A3u, ItemId = 0x09A8 };
        bank.AddItem(
            new UOItemEntity { Id = (Serial)0x400000A4u, ItemId = 0x0EED, Amount = 250, IsStackable = true },
            new(1, 1)
        );

        var character = new UOMobileEntity
        {
            Id = (Serial)0x000000A1u,
            Name = "buyer",
            Location = new(100, 100, 0),
            MapId = 0,
            BackpackId = backpack.Id
        };
        character.AddEquippedItem(ItemLayerType.Backpack, backpack.Id);
        character.AddEquippedItem(ItemLayerType.Bank, bank.Id);
        characters.Backpack = backpack;
        characters.BankBox = bank;

        var session = new GameSession(new(client))
        {
            Character = character,
            CharacterId = character.Id
        };
        sessions.Add(session);

        var vendor = new UOMobileEntity
        {
            Id = (Serial)0x000000A2u,
            Name = "smith",
            Location = new(101, 100, 0),
            MapId = 0
        };
        vendor.SetCustomString("sell_profile_id", "vendor.weaponsmith");
        mobiles.Mobiles[vendor.Id] = vendor;

        itemFactory.Templates["dagger"] = new() { Id = "dagger", Name = "Dagger", ItemId = "0x0F51" };
        sellProfiles.Upsert(
            new()
            {
                Id = "vendor.weaponsmith",
                Name = "Weaponsmith",
                VendorItems = [new() { ItemTemplateId = "dagger", Price = 12, MaxStock = 20 }]
            }
        );

        var service = new PlayerSellBuyService(
            sessions,
            mobiles,
            characters,
            itemFactory,
            itemService,
            sellProfiles,
            outgoing
        );

        await service.HandleVendorBuyRequestAsync(session.SessionId, vendor.Id);

        var statusPacket = DrainPackets(outgoing).OfType<PlayerStatusPacket>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(statusPacket.Mobile, Is.Not.Null);
                Assert.That(statusPacket.Mobile!.Gold, Is.EqualTo(1000));
            }
        );
    }

    [Test]
    public async Task HandleVendorSellRequestAsync_ShouldEnqueueClassicVendorSellList()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var sessions = new TestSessionService();
        var mobiles = new TestMobileService();
        var characters = new TestCharacterService();
        var itemFactory = new TestItemFactoryService();
        var itemService = new TestItemService();
        var sellProfiles = new SellProfileTemplateService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();

        var backpack = new UOItemEntity { Id = (Serial)0x40000091u, ItemId = 0x0E75 };
        var soldItem = new UOItemEntity { Id = (Serial)0x40000092u, ItemId = 0x0F51, Amount = 3, IsStackable = true };
        backpack.AddItem(soldItem, new(1, 1));

        var character = new UOMobileEntity
        {
            Id = (Serial)0x00000052u,
            Name = "seller",
            Location = new(100, 100, 0),
            MapId = 0,
            BackpackId = backpack.Id
        };
        character.AddEquippedItem(ItemLayerType.Backpack, backpack);
        characters.Backpack = backpack;

        var session = new GameSession(new(client))
        {
            Character = character,
            CharacterId = character.Id
        };
        sessions.Add(session);

        var vendor = new UOMobileEntity
        {
            Id = (Serial)0x00000088u,
            Name = "merchant",
            Location = new(101, 100, 0),
            MapId = 0
        };
        vendor.SetCustomString("sell_profile_id", "vendor.weaponsmith");
        mobiles.Mobiles[vendor.Id] = vendor;

        itemFactory.Templates["dagger"] = new() { Id = "dagger", Name = "Dagger", ItemId = "0x0F51" };
        sellProfiles.Upsert(
            new()
            {
                Id = "vendor.weaponsmith",
                Name = "Weaponsmith",
                AcceptedItems = [new() { ItemTemplateId = "dagger", Price = 6 }]
            }
        );

        var service = new PlayerSellBuyService(
            sessions,
            mobiles,
            characters,
            itemFactory,
            itemService,
            sellProfiles,
            outgoing
        );

        await service.HandleVendorSellRequestAsync(session.SessionId, vendor.Id);

        var packetTypes = DrainPacketTypes(outgoing);

        Assert.That(packetTypes, Does.Contain(typeof(VendorSellListPacket)));
    }

    private static List<object> DrainPackets(BasePacketListenerTestOutgoingPacketQueue outgoing)
    {
        var result = new List<object>();

        while (outgoing.TryDequeue(out var gamePacket))
        {
            result.Add(gamePacket.Packet);
        }

        return result;
    }

    private static List<Type> DrainPacketTypes(BasePacketListenerTestOutgoingPacketQueue outgoing)
    {
        var result = new List<Type>();

        while (outgoing.TryDequeue(out var gamePacket))
        {
            result.Add(gamePacket.Packet.GetType());
        }

        return result;
    }

    private static BuyItemsPacket ParseBuyItemsPacket(Serial vendorSerial, Serial itemSerial, short amount)
    {
        var writer = new SpanWriter(128, true);
        writer.Write((byte)0x3B);
        writer.Write((ushort)0);
        writer.Write((uint)vendorSerial);
        writer.Write((byte)0x02);
        writer.Write((byte)ItemLayerType.ShopBuy);
        writer.Write((uint)itemSerial);
        writer.Write(amount);
        writer.WritePacketLength();
        var bytes = writer.ToArray();
        writer.Dispose();

        var packet = new BuyItemsPacket();
        Assert.That(packet.TryParse(bytes), Is.True);

        return packet;
    }

    private static SellListReplyPacket ParseSellListReplyPacket(Serial vendorSerial, Serial itemSerial, short amount)
    {
        var writer = new SpanWriter(128, true);
        writer.Write((byte)0x9F);
        writer.Write((ushort)0);
        writer.Write((uint)vendorSerial);
        writer.Write((ushort)1);
        writer.Write((uint)itemSerial);
        writer.Write(amount);
        writer.WritePacketLength();
        var bytes = writer.ToArray();
        writer.Dispose();

        var packet = new SellListReplyPacket();
        Assert.That(packet.TryParse(bytes), Is.True);

        return packet;
    }
}
