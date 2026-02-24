using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Listeners.Base;
using Moongate.UO.Data.Geometry;
using Serilog;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.DropItemPacket),
 RegisterPacketHandler(PacketDefinition.PickUpItemPacket)
]
public class ItemHandler : BasePacketListener
{
    private readonly ILogger _logger = Log.ForContext<ItemHandler>();

    private readonly IItemService _itemService;

    private readonly ICharacterService _characterService;

    public ItemHandler(
        IOutgoingPacketQueue outgoingPacketQueue,
        IItemService itemService,
        ICharacterService characterService
    ) : base(outgoingPacketQueue)
    {
        _itemService = itemService;
        _characterService = characterService;
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

        var item = await _itemService.GetItemAsync(dropItemPacket.ItemSerial);

        if (!dropItemPacket.IsGroundDrop)
        {
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

        return true;
    }
}
