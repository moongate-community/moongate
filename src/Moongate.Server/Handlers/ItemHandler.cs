using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Listeners.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Serilog;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.DropItemPacket),
 RegisterPacketHandler(PacketDefinition.PickUpItemPacket)
]
public class ItemHandler : BasePacketListener
{
    private readonly ILogger _logger = Log.ForContext<ItemHandler>();

    private readonly IItemService _itemService;

    private readonly IGameEventBusService _gameEventBusService;

    public ItemHandler(
        IOutgoingPacketQueue outgoingPacketQueue,
        IItemService itemService,
        IGameEventBusService gameEventBusService
    ) : base(outgoingPacketQueue)
    {
        _itemService = itemService;
        _gameEventBusService = gameEventBusService;
    }

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is DropItemPacket dropItemPacket)
        {
            return await HandleDropItemAsync(session, dropItemPacket);
        }

        if (packet is PickUpItemPacket pickUpItemPacket)
        {
            return await HandlePickUpItemAsync(session, pickUpItemPacket);
        }

        return true;
    }

    private async Task<bool> HandlePickUpItemAsync(GameSession session, PickUpItemPacket pickUpItemPacket)
    {
        var item = await _itemService.GetItemAsync(pickUpItemPacket.ItemSerial);

        if (item.Amount > pickUpItemPacket.StackAmount)
        {
            var container = await _itemService.GetItemAsync(item.ParentContainerId);

            var clonedItem = await _itemService.CloneAsync(item.Id);

            item.Amount -= pickUpItemPacket.StackAmount;
            clonedItem.Amount = pickUpItemPacket.StackAmount;

            container.AddItem(clonedItem, item.ContainerPosition);

            await _itemService.UpsertItemsAsync(clonedItem, item, container);
        }

        return true;
    }

    private async Task<bool> HandleDropItemAsync(GameSession session, DropItemPacket dropItemPacket)
    {
        _logger.Information("Dropping item {@DropItemPacket}", dropItemPacket);

        if (!dropItemPacket.IsGroundDrop)
        {
            await DropItemInContainerAsync(session, dropItemPacket);

            return true;
        }

        await DropItemOnGroundAsync(session, dropItemPacket);

        return true;
    }

    private async Task DropItemOnGroundAsync(GameSession session, DropItemPacket dropItemPacket)
    {
        var mapId = session.Character?.MapId ?? 0;
        var dropResult = await _itemService.DropItemToGroundAsync(dropItemPacket.ItemSerial, dropItemPacket.Location, mapId);

        if (dropResult is null)
        {
            return;
        }

        await _gameEventBusService.PublishAsync(
            new DropItemToGroundEvent(
                session.SessionId,
                session.CharacterId,
                dropResult.Value.ItemId,
                dropResult.Value.SourceContainerId,
                dropResult.Value.OldLocation,
                dropResult.Value.NewLocation
            )
        );

        var sourceContainer = await _itemService.GetItemAsync(dropResult.Value.SourceContainerId);
        Enqueue(session, new DrawContainerAndAddItemCombinedPacket(sourceContainer));
    }

    private async Task DropItemInContainerAsync(GameSession session, DropItemPacket dropItemPacket)
    {
        var item = await _itemService.GetItemAsync(dropItemPacket.ItemSerial);

        var destinationContainer = await _itemService.GetItemAsync(dropItemPacket.DestinationSerial);
        var itemContainer = await _itemService.GetItemAsync(item.ParentContainerId);

        if (!destinationContainer.IsContainer &&
            destinationContainer.IsStackable &&
            destinationContainer.ItemId == item.ItemId)
        {
            // Check if destination container is stackable with the dropped item and stack if possible.
            destinationContainer.Amount += item.Amount;
            await _itemService.UpsertItemAsync(destinationContainer);
        }
        else
        {
            destinationContainer.AddItem(item, new Point2D(dropItemPacket.Location));
        }

        await _itemService.UpsertItemAsync(destinationContainer);

        if (itemContainer.Id != destinationContainer.Id)
        {
            itemContainer.RemoveItem(item.Id);
            await _itemService.UpsertItemAsync(itemContainer);
        }

        Enqueue(session, new DrawContainerAndAddItemCombinedPacket(destinationContainer));
    }
}
