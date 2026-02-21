using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Maps;
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
        => _itemService = itemService;

    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is DropItemPacket dropItemPacket)
        {
            await HandleDropItemAsync(session, dropItemPacket);

            return;
        }

        if (packet is PickUpItemPacket pickUpItemPacket)
        {
            await HandlePickUpItemAsync(session, pickUpItemPacket);

            return;
        }

        if (packet is DropWearItemPacket dropWearItemPacket)
        {
            await HandleDropWearItemAsync(session, dropWearItemPacket);
        }
    }

    private async Task HandleDropItemAsync(GameSession session, DropItemPacket packet)
    {
        var droppingItem = _itemService.GetItem(packet.ItemId);

        if (droppingItem == null)
        {
            _logger.Warning("Item {ItemId} not found", packet.ItemId);
            return;
        }

        _logger.Information("Dropping item {DroppingItemId} on ground: {Ground}", droppingItem.Name, packet.IsGround);

        droppingItem.Map = session.Mobile.Map;

        if (packet.IsGround)
        {
            _logger.Debug("Dropping item {ItemId} on ground at location {Location}", droppingItem.Id, packet.Location);
            droppingItem.ParentId = Serial.Zero;
            droppingItem.MoveTo(packet.Location, true);

            session.SendPackets(new DropItemApprovedPacket());

            return;
        }

        droppingItem.ParentId = packet.ContainerId;

        droppingItem.MoveTo(packet.Location, false);

        var parentContainer = _itemService.GetItem(packet.ContainerId);

        if (parentContainer == null)
        {
            _logger.Warning("Container {ContainerId} not found", packet.ContainerId);
            return;
        }

        if (!parentContainer.ContainsItem(droppingItem))
        {
            _logger.Information("Adding item {ItemId} to container {ContainerId}", droppingItem.Id, parentContainer.Id);
            parentContainer.AddItem(droppingItem, new(packet.Location.X, packet.Location.Y));
        }

        session.SendPackets(new DropItemApprovedPacket());
        session.SendPackets(new AddMultipleItemToContainerPacket(parentContainer));
    }

    private async Task HandleDropWearItemAsync(GameSession session, DropWearItemPacket packet)
    {
        var mobile = session.Mobile;

        if (mobile.Id != packet.MobileId)
        {
            _logger.Warning("Player {PlayerId} attempted to drop item on another player", session.Mobile.Id);

            return;
        }

        var droppingItem = _itemService.GetItem(packet.ItemId);

        if (droppingItem == null)
        {
            _logger.Warning("Item {ItemId} not found", packet.ItemId);
            return;
        }

        mobile.AddItem(packet.Layer, droppingItem);
        droppingItem.ParentId = mobile.Id;
        droppingItem.Map = Map.Felucca;

        _itemService.RemoveItemFromWorld(droppingItem);

        _logger.Information(
            "Wear groud item {DroppingItemId} on layer {Layer} for mobile {MobileId}",
            droppingItem.Name,
            packet.Layer,
            packet.MobileId
        );
    }

    private async Task HandlePickUpItemAsync(GameSession session, PickUpItemPacket packet)
    {
        var item = _itemService.GetItem(packet.ItemSerial);

        if (item == null)
        {
            _logger.Warning("Item {ItemId} not found", packet.ItemSerial);
            return;
        }

        if (!item.IsMovable)
        {
            _logger.Debug("Item {ItemId} is not movable", item.Id);

            // Log warning if item is not movable
        }

        _logger.Information("Picking up {Count} of {ItemName}", packet.StackAmount, item.Name);

        // Logic to add the item to the player's inventory or handle it accordingly
    }
}
