using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Items;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Handlers;

public class ItemHandlerTests
{
    [Test]
    public async Task HandlePacketAsync_ShouldPublishItemSingleClickEvent()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new ItemHandler(
            new BasePacketListenerTestOutgoingPacketQueue(),
            new ItemHandlerTestItemService(),
            eventBus,
            new FakeGameNetworkSessionService()
        );
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var targetSerial = (Serial)0x40000010u;
        var packet = new SingleClickPacket
        {
            TargetSerial = targetSerial
        };

        var handled = await handler.HandlePacketAsync(session, packet);
        var singleClickEvent = eventBus.Events.OfType<ItemSingleClickEvent>().FirstOrDefault();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(singleClickEvent.ItemSerial, Is.EqualTo(targetSerial));
                Assert.That(singleClickEvent.SessionId, Is.EqualTo(session.SessionId));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldPublishItemDoubleClickEvent()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new ItemHandler(
            new BasePacketListenerTestOutgoingPacketQueue(),
            new ItemHandlerTestItemService(),
            eventBus,
            new FakeGameNetworkSessionService()
        );
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var targetSerial = (Serial)0x40000020u;
        var packet = new DoubleClickPacket
        {
            TargetSerial = targetSerial
        };

        var handled = await handler.HandlePacketAsync(session, packet);
        var doubleClickEvent = eventBus.Events.OfType<ItemDoubleClickEvent>().FirstOrDefault();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(doubleClickEvent.ItemSerial, Is.EqualTo(targetSerial));
                Assert.That(doubleClickEvent.SessionId, Is.EqualTo(session.SessionId));
            }
        );
    }

    private sealed class ItemHandlerTestItemService : IItemService
    {
        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => throw new NotSupportedException();

        public Task<bool> DeleteItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task UpsertItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => throw new NotSupportedException();
    }
}
