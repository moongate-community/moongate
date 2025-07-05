using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Packets.Items;
using Moongate.UO.Data.Session;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Handlers;
using Serilog;

namespace Moongate.UO.PacketHandlers;

public class ItemsHandler : IGamePacketHandler
{
    private readonly IItemService _itemService;
    private readonly ILogger _logger = Log.ForContext<ItemsHandler>();

    public ItemsHandler(IItemService itemService)
    {
        _itemService = itemService;
    }

    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is DropItemPacket dropItemPacket)
        {
            await HandleDropItemAsync(session, dropItemPacket);
        }
        else if (packet is PickUpItemPacket pickUpItemPacket)
        {
            await HandlePickUpItemAsync(session, pickUpItemPacket);
        }
    }

    private async Task HandleDropItemAsync(GameSession session, DropItemPacket packet)
    {
        var droppingItem = _itemService.GetItem(packet.ItemId);

        _logger.Information("Dropping item {DroppingItemId} on ground: {Ground}", droppingItem.Name, packet.IsGround);

        if (session.Mobile.Location.GetDistance(packet.Location) <= 2 && packet.IsGround)
        {
            droppingItem.ParentId = Serial.Zero;
            droppingItem.MoveTo(packet.Location);

            session.SendPackets(new DropItemApprovedPacket());

            return;
        }

        droppingItem.ParentId = packet.ContainerId;

        droppingItem.MoveTo(packet.Location);

        var parentContainer = _itemService.GetItem(packet.ContainerId);

        if (!parentContainer.ContainsItem(droppingItem))
        {
            _logger.Information("Adding item {ItemId} to container {ContainerId}", droppingItem.Id, parentContainer.Id);
            parentContainer.AddItem(droppingItem, new Point2D(packet.Location.X, packet.Location.Y));
        }

        session.SendPackets(new DropItemApprovedPacket());
        session.SendPackets(new AddMultipleItemToContainerPacket(parentContainer));
    }

    private async Task HandlePickUpItemAsync(GameSession session, PickUpItemPacket packet)
    {
        var item = _itemService.GetItem(packet.ItemSerial);


        _logger.Information("Picking up {Count} of {ItemName}", packet.StackAmount, item.Name);

        // Logic to add the item to the player's inventory or handle it accordingly
    }
}
