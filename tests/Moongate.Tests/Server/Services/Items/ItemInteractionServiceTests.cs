using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Books;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Items;
using Moongate.Server.Services.Items;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Items;

public class ItemInteractionServiceTests
{
    private sealed class TestItemService : IItemService
    {
        public Dictionary<Serial, UOItemEntity> ItemsById { get; } = [];

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task<bool> DeleteItemAsync(Serial itemId)
            => throw new NotSupportedException();

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
            => Task.FromResult(ItemsById.TryGetValue(itemId, out var item) ? item : null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task UpsertItemAsync(UOItemEntity item)
            => Task.CompletedTask;

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;
    }

    private sealed class TestBookService : IItemBookService
    {
        public bool TryEnqueueBookCalled { get; private set; }
        public UOItemEntity? LastItem { get; private set; }
        public bool TryEnqueueBookResult { get; set; }

        public Task<bool> HandleBookHeaderAsync(
            GameSession session,
            BookHeaderNewPacket packet,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();

        public Task<bool> HandleBookPagesAsync(
            GameSession session,
            BookPagesPacket packet,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();

        public Task<bool> TryEnqueueBookAsync(
            GameSession session,
            UOItemEntity item,
            CancellationToken cancellationToken = default
        )
        {
            _ = session;
            _ = cancellationToken;
            TryEnqueueBookCalled = true;
            LastItem = item;

            return Task.FromResult(TryEnqueueBookResult);
        }
    }

    private sealed class TestCharacterService : ICharacterService
    {
        public UOMobileEntity? Character { get; set; }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => throw new NotSupportedException();

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => throw new NotSupportedException();

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
            => throw new NotSupportedException();

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult(Character);

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => throw new NotSupportedException();

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => throw new NotSupportedException();
    }

    private sealed class TestScriptDispatcher : IItemScriptDispatcher
    {
        public bool HasHookResult { get; set; } = true;

        public Task<bool> DispatchAsync(ItemScriptContext context, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public bool HasHook(UOItemEntity item, string hookName)
            => HasHookResult;
    }

    [Test]
    public async Task HandleDoubleClickAsync_ShouldPublishItemDoubleClickEvent()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new TestItemService();
        var bookService = new TestBookService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = new ItemInteractionService(itemService, eventBus, bookService, queue);
        var targetSerial = (Serial)0x40000020u;
        itemService.ItemsById[targetSerial] = new() { Id = targetSerial, ParentContainerId = (Serial)0x40000001u };
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await service.HandleDoubleClickAsync(session, new() { TargetSerial = targetSerial });

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(eventBus.Events.OfType<ItemDoubleClickEvent>().Single().ItemSerial, Is.EqualTo(targetSerial));
            }
        );
    }

    [Test]
    public async Task HandleDoubleClickAsync_WhenContainer_ShouldEnqueueContainerPacket()
    {
        TileData.ItemTable[0x0E75] = new(string.Empty, UOTileFlag.Container, 0, 0, 0, 0, 0, 0);
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new TestItemService();
        var bookService = new TestBookService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = new ItemInteractionService(itemService, eventBus, bookService, queue);
        var targetSerial = (Serial)0x40000030u;
        itemService.ItemsById[targetSerial] = new()
        {
            Id = targetSerial,
            ItemId = 0x0E75,
            ParentContainerId = (Serial)0x40000001u
        };
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await service.HandleDoubleClickAsync(session, new() { TargetSerial = targetSerial });

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<DrawContainerAndAddItemCombinedPacket>());
            }
        );
    }

    [Test]
    public async Task HandleDoubleClickAsync_WhenCorpseContainer_ShouldEnqueueCorpseClothingPacket()
    {
        TileData.ItemTable[0x2006] = new(string.Empty, UOTileFlag.Container, 0, 0, 0, 0, 0, 0);
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new TestItemService();
        var bookService = new TestBookService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = new ItemInteractionService(itemService, eventBus, bookService, queue);
        var corpseId = (Serial)0x40000031u;
        var chestId = (Serial)0x40000032u;
        var corpse = new UOItemEntity
        {
            Id = corpseId,
            ItemId = 0x2006,
            ParentContainerId = (Serial)0x40000001u
        };
        corpse.SetCustomBoolean("is_corpse", true);
        var chest = new UOItemEntity
        {
            Id = chestId,
            ItemId = 0x1415,
            ParentContainerId = corpseId
        };
        corpse.AddItem(chest, Point2D.Zero);
        corpse.Items[0].SetCustomInteger("corpse_equipped_layer", (byte)ItemLayerType.InnerTorso);
        itemService.ItemsById[corpseId] = corpse;
        itemService.ItemsById[chestId] = chest;
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await service.HandleDoubleClickAsync(session, new() { TargetSerial = corpseId });

        var packetTypes = new List<Type>();

        while (queue.TryDequeue(out var outbound))
        {
            packetTypes.Add(outbound.Packet.GetType());
        }

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(packetTypes, Does.Contain(typeof(DrawContainerAndAddItemCombinedPacket)));
                Assert.That(packetTypes, Does.Contain(typeof(CorpseClothingPacket)));
            }
        );
    }

    [Test]
    public async Task HandleDoubleClickAsync_WhenDoubleClickItemWithoutScriptHook_ShouldNotPublishItemDoubleClickEvent()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new TestItemService();
        var bookService = new TestBookService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var scriptDispatcher = new TestScriptDispatcher { HasHookResult = false };
        var service = new ItemInteractionService(itemService, eventBus, bookService, queue, scriptDispatcher);
        var targetSerial = (Serial)0x40000066u;
        itemService.ItemsById[targetSerial] = new() { Id = targetSerial, ParentContainerId = (Serial)0x40000001u };
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await service.HandleDoubleClickAsync(session, new() { TargetSerial = targetSerial });

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(eventBus.Events.OfType<ItemDoubleClickEvent>(), Is.Empty);
            }
        );
    }

    [Test]
    public async Task HandleDoubleClickAsync_WhenMobile_ShouldPublishEventAndEnqueuePaperdollForHumanoid()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new TestItemService();
        var bookService = new TestBookService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var characterService = new TestCharacterService
        {
            Character = new()
            {
                Id = (Serial)0x33u
            }
        };
        var service = new ItemInteractionService(itemService, eventBus, bookService, queue, null, characterService);
        var targetSerial = (Serial)0x00000033u;
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await service.HandleDoubleClickAsync(session, new() { TargetSerial = targetSerial });

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(
                    eventBus.Events.OfType<MobileDoubleClickEvent>().Single().MobileSerial,
                    Is.EqualTo(targetSerial)
                );
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<PaperdollPacket>());
            }
        );
    }

    [Test]
    public async Task HandleDoubleClickAsync_WhenReadonlyBook_ShouldDelegateToItemBookService()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new TestItemService();
        var bookService = new TestBookService { TryEnqueueBookResult = true };
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = new ItemInteractionService(itemService, eventBus, bookService, queue);
        var targetSerial = (Serial)0x40000021u;
        itemService.ItemsById[targetSerial] = new() { Id = targetSerial, ParentContainerId = (Serial)0x40000001u };
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await service.HandleDoubleClickAsync(session, new() { TargetSerial = targetSerial });

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(bookService.TryEnqueueBookCalled, Is.True);
                Assert.That(bookService.LastItem?.Id, Is.EqualTo(targetSerial));
            }
        );
    }

    [Test]
    public async Task HandleDoubleClickAsync_WhenRegularSpellbook_ShouldEnqueueSpellbookPacket()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new TestItemService();
        var bookService = new TestBookService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = new ItemInteractionService(itemService, eventBus, bookService, queue);
        var targetSerial = (Serial)0x40000022u;
        var spellbook = new UOItemEntity
        {
            Id = targetSerial,
            ItemId = 0x0EFA,
            ParentContainerId = (Serial)0x40000001u
        };
        spellbook.SetCustomString(ItemCustomParamKeys.Item.TemplateId, "spellbook");
        spellbook.SetCustomInteger(ItemCustomParamKeys.Spellbook.Content, 0x0000000000000003L);
        spellbook.SetCustomString(ItemCustomParamKeys.Book.Title, "Legacy");
        itemService.ItemsById[targetSerial] = spellbook;
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await service.HandleDoubleClickAsync(session, new() { TargetSerial = targetSerial });

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(bookService.TryEnqueueBookCalled, Is.False);
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<DisplaySpellbookAndContentsPacket>());
            }
        );
    }

    [Test]
    public async Task HandleSingleClickAsync_ShouldPublishItemSingleClickEvent()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new TestItemService();
        var bookService = new TestBookService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = new ItemInteractionService(itemService, eventBus, bookService, queue);
        var targetSerial = (Serial)0x40000010u;
        itemService.ItemsById[targetSerial] = new() { Id = targetSerial, ParentContainerId = (Serial)0x40000001u };
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await service.HandleSingleClickAsync(session, new() { TargetSerial = targetSerial });

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(eventBus.Events.OfType<ItemSingleClickEvent>().Single().ItemSerial, Is.EqualTo(targetSerial));
            }
        );
    }

    [Test]
    public async Task HandleSingleClickAsync_WhenGroundItemOutOfRangeAndRegular_ShouldNotPublishEvent()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new TestItemService();
        var bookService = new TestBookService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = new ItemInteractionService(itemService, eventBus, bookService, queue);
        var targetSerial = (Serial)0x40000023u;
        itemService.ItemsById[targetSerial] = new()
        {
            Id = targetSerial,
            MapId = 0,
            Location = new(100, 100, 0),
            ParentContainerId = Serial.Zero,
            EquippedMobileId = Serial.Zero
        };
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            AccountType = AccountType.Regular,
            Character = new() { Id = (Serial)0x2u, MapId = 0, Location = new(10, 10, 0) }
        };

        var handled = await service.HandleSingleClickAsync(session, new() { TargetSerial = targetSerial });

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(eventBus.Events.OfType<ItemSingleClickEvent>(), Is.Empty);
            }
        );
    }
}
