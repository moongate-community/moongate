using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Books;
using Moongate.Network.Packets.Outgoing.System;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Services.Items;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Items;

public class ItemBookServiceTests
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
        {
            ItemsById[item.Id] = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
        {
            foreach (var item in items)
            {
                ItemsById[item.Id] = item;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestMobileService : IMobileService
    {
        public UOMobileEntity? Mobile { get; set; }

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(Mobile ?? new UOMobileEntity { Id = id });

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
            int mapId,
            int sectorX,
            int sectorY,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();

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

    [Test]
    public async Task HandleBookHeaderAsync_WhenWritableBookInBackpack_ShouldPersistTitleAndAuthorAndEnqueueTooltip()
    {
        var itemService = new TestItemService();
        var backpackId = (Serial)0x40000001u;
        var playerId = (Serial)0x00000002u;
        var mobileService = new TestMobileService
        {
            Mobile = new()
            {
                Id = playerId,
                BackpackId = backpackId
            }
        };
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = new ItemBookService(itemService, mobileService, queue);
        itemService.ItemsById[backpackId] = new() { Id = backpackId, ItemId = 0x0E75 };
        var item = new UOItemEntity
        {
            Id = (Serial)0x40000024u,
            ItemId = 0x0FF0,
            ParentContainerId = backpackId
        };
        item.SetCustomString("book_title", "Old Title");
        item.SetCustomString("book_author", "Old Author");
        item.SetCustomString("book_content", "Line 1");
        item.SetCustomString("book_writable", "true");
        itemService.ItemsById[item.Id] = item;
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = playerId,
            Character = new() { Id = playerId }
        };

        var handled = await service.HandleBookHeaderAsync(
                          session,
                          new()
                          {
                              BookSerial = item.Id.Value,
                              Title = "New Title",
                              Author = "New Author"
                          }
                      );

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(item.TryGetCustomString("book_title", out var title), Is.True);
                Assert.That(title, Is.EqualTo("New Title"));
                Assert.That(item.TryGetCustomString("book_author", out var author), Is.True);
                Assert.That(author, Is.EqualTo("New Author"));
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<ObjectPropertyList>());
            }
        );
    }

    [Test]
    public async Task HandleBookHeaderAsync_WhenWritableBookIsOutsideBackpackOrEquipment_ShouldIgnoreUpdate()
    {
        var itemService = new TestItemService();
        var playerId = (Serial)0x00000002u;
        var mobileService = new TestMobileService
        {
            Mobile = new()
            {
                Id = playerId,
                BackpackId = (Serial)0x40000001u
            }
        };
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = new ItemBookService(itemService, mobileService, queue);
        var item = new UOItemEntity
        {
            Id = (Serial)0x40000026u,
            ItemId = 0x0FF0,
            ParentContainerId = Serial.Zero,
            EquippedMobileId = Serial.Zero
        };
        item.SetCustomString("book_title", "Journal");
        item.SetCustomString("book_author", "Tommy");
        item.SetCustomString("book_content", "Line 1");
        item.SetCustomString("book_writable", "true");
        itemService.ItemsById[item.Id] = item;
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = playerId,
            Character = new() { Id = playerId }
        };

        var handled = await service.HandleBookHeaderAsync(
                          session,
                          new()
                          {
                              BookSerial = item.Id.Value,
                              Title = "Blocked",
                              Author = "Blocked"
                          }
                      );

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(item.TryGetCustomString("book_title", out var title), Is.True);
                Assert.That(title, Is.EqualTo("Journal"));
                Assert.That(item.TryGetCustomString("book_author", out var author), Is.True);
                Assert.That(author, Is.EqualTo("Tommy"));
                Assert.That(queue.TryDequeue(out _), Is.False);
            }
        );
    }

    [Test]
    public async Task HandleBookPagesAsync_WhenReadonlyPageIsRequested_ShouldEnqueueRequestedPage()
    {
        var itemService = new TestItemService();
        var mobileService = new TestMobileService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = new ItemBookService(itemService, mobileService, queue);
        var item = new UOItemEntity
        {
            Id = (Serial)0x40000022u,
            ItemId = 0x0FF0
        };
        item.SetCustomString("book_title", "Welcome");
        item.SetCustomString("book_author", "Archivist");
        item.SetCustomString("book_content", "Line 1\nLine 2\nLine 3");
        itemService.ItemsById[item.Id] = item;
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await service.HandleBookPagesAsync(
                          session,
                          new()
                          {
                              BookSerial = item.Id.Value,
                              PageCount = 1,
                              Pages =
                              {
                                  new()
                                  {
                                      PageNumber = 1,
                                      LineCount = 0xFFFF
                                  }
                              }
                          }
                      );

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<BookPagesPacket>());
                var pages = (BookPagesPacket)outbound.Packet;
                Assert.That(pages.Pages, Has.Count.EqualTo(1));
                Assert.That(pages.Pages[0].Lines, Is.EqualTo(new[] { "Line 1", "Line 2", "Line 3" }));
            }
        );
    }

    [Test]
    public async Task HandleBookPagesAsync_WhenWritableBookPageContentIsSaved_ShouldPersistUpdatedLinesAndEnqueueTooltip()
    {
        var itemService = new TestItemService();
        var backpackId = (Serial)0x40000001u;
        var playerId = (Serial)0x00000002u;
        var mobileService = new TestMobileService
        {
            Mobile = new()
            {
                Id = playerId,
                BackpackId = backpackId
            }
        };
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = new ItemBookService(itemService, mobileService, queue);
        itemService.ItemsById[backpackId] = new() { Id = backpackId, ItemId = 0x0E75 };
        var item = new UOItemEntity
        {
            Id = (Serial)0x40000025u,
            ItemId = 0x0FF0,
            ParentContainerId = backpackId
        };
        item.SetCustomString("book_title", "Journal");
        item.SetCustomString("book_author", "Tommy");
        item.SetCustomString("book_content", "A1\nA2\nA3\nA4\nA5\nA6\nA7\nA8\nB1\nB2");
        item.SetCustomString("book_writable", "true");
        itemService.ItemsById[item.Id] = item;
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = playerId,
            Character = new() { Id = playerId }
        };

        var handled = await service.HandleBookPagesAsync(
                          session,
                          new()
                          {
                              BookSerial = item.Id.Value,
                              PageCount = 1,
                              Pages =
                              {
                                  new()
                                  {
                                      PageNumber = 2,
                                      LineCount = 2,
                                      Lines = { "Updated B1", "Updated B2" }
                                  }
                              }
                          }
                      );

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(item.TryGetCustomString("book_content", out var content), Is.True);
                Assert.That(content, Is.EqualTo("A1\nA2\nA3\nA4\nA5\nA6\nA7\nA8\nUpdated B1\nUpdated B2"));
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<ObjectPropertyList>());
            }
        );
    }

    [Test]
    public async Task TryEnqueueBookAsync_WhenReadonlyBook_ShouldEnqueueHeaderAndPages()
    {
        var itemService = new TestItemService();
        var mobileService = new TestMobileService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = new ItemBookService(itemService, mobileService, queue);
        var item = new UOItemEntity
        {
            Id = (Serial)0x40000021u,
            ItemId = 0x0FF0
        };
        item.SetCustomString("book_title", "Welcome");
        item.SetCustomString("book_author", "Archivist");
        item.SetCustomString("book_content", "Line 1\nLine 2\nLine 3");
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await service.TryEnqueueBookAsync(session, item);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(queue.TryDequeue(out var headerOutbound), Is.True);
                Assert.That(headerOutbound.Packet, Is.TypeOf<BookHeaderNewPacket>());
                Assert.That(queue.TryDequeue(out var pagesOutbound), Is.True);
                Assert.That(pagesOutbound.Packet, Is.TypeOf<BookPagesPacket>());
            }
        );
    }
}
