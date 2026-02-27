using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Listeners.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Serilog;

namespace Moongate.Server.Handlers;

[
    RegisterGameEventListener,
    RegisterPacketHandler(PacketDefinition.DropItemPacket),
    RegisterPacketHandler(PacketDefinition.PickUpItemPacket),
    RegisterPacketHandler(PacketDefinition.SingleClickPacket),
    RegisterPacketHandler(PacketDefinition.DoubleClickPacket)
]
public class ItemHandler : BasePacketListener, IGameEventListener<ItemMovedEvent>
{
    private readonly ILogger _logger = Log.ForContext<ItemHandler>();

    private readonly IItemService _itemService;

    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    private readonly IGameEventBusService _gameEventBusService;
    private readonly IPlayerDragService _playerDragService;

    public ItemHandler(
        IOutgoingPacketQueue outgoingPacketQueue,
        IItemService itemService,
        IGameEventBusService gameEventBusService,
        IGameNetworkSessionService gameNetworkSessionService,
        IPlayerDragService playerDragService
    ) : base(outgoingPacketQueue)
    {
        _itemService = itemService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _playerDragService = playerDragService;
        {
            _gameEventBusService = gameEventBusService;
        }
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

        if (packet is SingleClickPacket singleClickPacket)
        {
            return await HandleSingleClickAsync(session, singleClickPacket);
        }

        if (packet is DoubleClickPacket doubleClickPacket)
        {
            return await HandleDoubleClickAsync(session, doubleClickPacket);
        }

        return true;
    }

    private async Task<bool> HandleDoubleClickAsync(GameSession session, DoubleClickPacket doubleClickPacket)
    {
        await _gameEventBusService.PublishAsync(
            new ItemDoubleClickEvent(
                session.SessionId,
                doubleClickPacket.TargetSerial
            )
        );

        return true;
    }

    private async Task<bool> HandleSingleClickAsync(GameSession session, SingleClickPacket singleClickPacket)
    {
        await _gameEventBusService.PublishAsync(
            new ItemSingleClickEvent(
                session.SessionId,
                singleClickPacket.TargetSerial
            )
        );

        return true;
    }

    private async Task DropItemInContainerAsync(GameSession session, DropItemPacket dropItemPacket)
    {
        var item = await _itemService.GetItemAsync(dropItemPacket.ItemSerial);
        if (item is null)
        {
            return;
        }

        var destinationContainer = await _itemService.GetItemAsync(dropItemPacket.DestinationSerial);
        if (destinationContainer is null)
        {
            return;
        }

        if (!destinationContainer.IsContainer &&
            destinationContainer.IsStackable &&
            destinationContainer.ItemId == item.ItemId)
        {
            // Check if destination container is stackable with the dropped item and stack if possible.
            destinationContainer.Amount += item.Amount;
            await _itemService.UpsertItemAsync(destinationContainer);
            await _itemService.DeleteItemAsync(item.Id);
        }
        else
        {
            await _itemService.MoveItemToContainerAsync(
                item.Id,
                destinationContainer.Id,
                new(dropItemPacket.Location.X, dropItemPacket.Location.Y),
                session.SessionId
            );
        }

        destinationContainer = await _itemService.GetItemAsync(destinationContainer.Id);

        if (destinationContainer is null)
        {
            return;
        }

        Enqueue(session, new DrawContainerAndAddItemCombinedPacket(destinationContainer));
    }

    private async Task DropItemOnGroundAsync(GameSession session, DropItemPacket dropItemPacket)
    {
        var mapId = session.Character?.MapId ?? 0;
        var dropResult = await _itemService.DropItemToGroundAsync(
                             dropItemPacket.ItemSerial,
                             dropItemPacket.Location,
                             mapId,
                             session.SessionId
                         );

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

    private async Task<bool> HandleDropItemAsync(GameSession session, DropItemPacket dropItemPacket)
    {
        _logger.Information("Dropping item {@DropItemPacket}", dropItemPacket);
        if (!_playerDragService.TryGet(session.SessionId, out var dragState) || dragState.ItemId != dropItemPacket.ItemSerial)
        {
            _logger.Warning(
                "Drop rejected Session={SessionId} ItemId={ItemId}: no matching pending drag state",
                session.SessionId,
                dropItemPacket.ItemSerial
            );

            return false;
        }

        if (!dropItemPacket.IsGroundDrop)
        {
            await DropItemInContainerAsync(session, dropItemPacket);
            _playerDragService.Clear(session.SessionId);

            return true;
        }

        await DropItemOnGroundAsync(session, dropItemPacket);
        _playerDragService.Clear(session.SessionId);

        return true;
    }

    private async Task<bool> HandlePickUpItemAsync(GameSession session, PickUpItemPacket pickUpItemPacket)
    {
        var item = await _itemService.GetItemAsync(pickUpItemPacket.ItemSerial);
        if (item is null)
        {
            return false;
        }

        var requestedAmount = Math.Max(1, pickUpItemPacket.StackAmount);
        var pickedAmount = Math.Min(requestedAmount, Math.Max(1, item.Amount));

        if (item.Amount > pickedAmount)
        {
            var container = await _itemService.GetItemAsync(item.ParentContainerId);
            if (container is null)
            {
                return false;
            }

            var clonedItem = await _itemService.CloneAsync(item.Id);
            if (clonedItem is null)
            {
                return false;
            }

            item.Amount -= pickedAmount;
            clonedItem.Amount = pickedAmount;
            clonedItem.ParentContainerId = Serial.Zero;
            clonedItem.ContainerPosition = Point2D.Zero;
            clonedItem.Location = item.Location;

            await _itemService.UpsertItemsAsync(clonedItem, item);

            _playerDragService.SetPending(
                session.SessionId,
                clonedItem.Id,
                clonedItem.Amount,
                item.ParentContainerId,
                item.Location
            );

            return true;
        }

        _playerDragService.SetPending(
            session.SessionId,
            item.Id,
            pickedAmount,
            item.ParentContainerId,
            item.Location
        );

        return true;
    }

    public async Task HandleAsync(ItemMovedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        if (_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session))
        {
            var item = await _itemService.GetItemAsync(gameEvent.NewContainerId);

            if (item is null)
            {
                return;
            }

            var container = await _itemService.GetItemAsync(gameEvent.NewContainerId);

            Enqueue(session, new DrawContainerAndAddItemCombinedPacket(container));
        }
    }
}
