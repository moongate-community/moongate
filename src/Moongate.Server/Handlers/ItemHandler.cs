using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Internal.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Listeners.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Handlers;

[
    RegisterGameEventListener,
    RegisterPacketHandler(PacketDefinition.DropItemPacket),
    RegisterPacketHandler(PacketDefinition.DropWearItemPacket),
    RegisterPacketHandler(PacketDefinition.PickUpItemPacket),
    RegisterPacketHandler(PacketDefinition.SingleClickPacket),
    RegisterPacketHandler(PacketDefinition.DoubleClickPacket)
]
public class ItemHandler : BasePacketListener, IGameEventListener<ItemMovedEvent>
{
    private readonly ILogger _logger = Log.ForContext<ItemHandler>();

    private readonly IItemService _itemService;

    private readonly IMobileService _mobileService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    private readonly IGameEventBusService _gameEventBusService;
    private readonly IPlayerDragService _playerDragService;

    private readonly ISpatialWorldService _spatialWorldService;

    public ItemHandler(
        IOutgoingPacketQueue outgoingPacketQueue,
        IItemService itemService,
        IGameEventBusService gameEventBusService,
        IGameNetworkSessionService gameNetworkSessionService,
        IPlayerDragService playerDragService,
        ISpatialWorldService spatialWorldService,
        IMobileService mobileService
    ) : base(outgoingPacketQueue)
    {
        _itemService = itemService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _playerDragService = playerDragService;
        _spatialWorldService = spatialWorldService;
        _mobileService = mobileService;
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

        if (packet is DropWearItemPacket dropWearItemPacket)
        {
            return await HandleDropWearItemAsync(session, dropWearItemPacket);
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

    private async Task<bool> HandleDropWearItemAsync(GameSession session, DropWearItemPacket dropWearItemPacket)
    {
        if (session.Character is null || session.CharacterId == Serial.Zero)
        {
            return false;
        }

        if (dropWearItemPacket.PlayerSerial != session.CharacterId)
        {
            _logger.Warning(
                "DropWear rejected Session={SessionId} ItemId={ItemId}: target player mismatch packet={PacketPlayerId} session={SessionPlayerId}",
                session.SessionId,
                dropWearItemPacket.ItemSerial,
                dropWearItemPacket.PlayerSerial,
                session.CharacterId
            );

            return false;
        }

        if (!IsValidWearLayer(dropWearItemPacket.Layer))
        {
            _logger.Warning(
                "DropWear rejected Session={SessionId} ItemId={ItemId}: invalid requested layer {Layer}",
                session.SessionId,
                dropWearItemPacket.ItemSerial,
                dropWearItemPacket.Layer
            );

            return false;
        }

        await _itemService.EquipItemAsync(
            dropWearItemPacket.ItemSerial,
            session.CharacterId,
            dropWearItemPacket.Layer
        );

        await DispatchItemWearChange(session.CharacterId);

        return true;
    }

    private async Task<bool> HandleDoubleClickAsync(GameSession session, DoubleClickPacket doubleClickPacket)
    {
        if (doubleClickPacket.TargetSerial.IsMobile)
        {
            return true;
        }

        await _gameEventBusService.PublishAsync(
            new ItemDoubleClickEvent(
                session.SessionId,
                doubleClickPacket.TargetSerial
            )
        );



        var item = await _itemService.GetItemAsync(doubleClickPacket.TargetSerial);

        if (item is null)
        {
            return true;
        }

        if (item.IsContainer)
        {
            var container = await _itemService.GetItemAsync(item.Id);

            if (container is not null)
            {
                Enqueue(session, new DrawContainerAndAddItemCombinedPacket(container));
            }
        }


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

        var containerToRefreshId = destinationContainer.Id;

        if (!destinationContainer.IsContainer &&
            destinationContainer.IsStackable &&
            destinationContainer.ItemId == item.ItemId)
        {
            // Check if destination container is stackable with the dropped item and stack if possible.
            destinationContainer.Amount += item.Amount;
            await _itemService.UpsertItemAsync(destinationContainer);
            await _itemService.DeleteItemAsync(item.Id);
            if (destinationContainer.ParentContainerId != Serial.Zero)
            {
                containerToRefreshId = destinationContainer.ParentContainerId;
            }
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

        destinationContainer = await _itemService.GetItemAsync(containerToRefreshId);

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

        if (dropResult.Value.SourceContainerId == Serial.Zero)
        {
            return;
        }

        var sourceContainer = await _itemService.GetItemAsync(dropResult.Value.SourceContainerId);
        Enqueue(session, new DrawContainerAndAddItemCombinedPacket(sourceContainer));
    }

    private async Task<bool> HandleDropItemAsync(GameSession session, DropItemPacket dropItemPacket)
    {
        if (!_playerDragService.TryGet(session.SessionId, out var dragState) ||
            dragState.ItemId != dropItemPacket.ItemSerial)
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
            var sourceContainerId = item.ParentContainerId;
            var sourceLocation = item.Location;
            var sourceContainerPosition = item.ContainerPosition;
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

            // Keep original serial as the dragged stack so DropItem packet item serial still matches pending drag state.
            item.Amount = pickedAmount;
            item.ParentContainerId = Serial.Zero;
            item.ContainerPosition = Point2D.Zero;
            item.Location = sourceLocation;

            // Persist the remainder in the original container with a new serial.
            clonedItem.Amount = Math.Max(1, clonedItem.Amount - pickedAmount);
            clonedItem.ParentContainerId = sourceContainerId;
            clonedItem.ContainerPosition = sourceContainerPosition;
            clonedItem.Location = sourceLocation;

            await _itemService.UpsertItemsAsync(clonedItem, item);

            _playerDragService.SetPending(
                session.SessionId,
                item.Id,
                item.Amount,
                sourceContainerId,
                sourceLocation
            );

            if (sourceContainerId != Serial.Zero)
            {
                var sourceContainer = await _itemService.GetItemAsync(sourceContainerId);

                if (sourceContainer is not null)
                {
                    Enqueue(session, new DrawContainerAndAddItemCombinedPacket(sourceContainer));
                }
            }

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

    private async Task DispatchItemWearChange(Serial characterId)
    {
        var mobile = await _mobileService.GetAsync(characterId);

        if (mobile is null)
        {
            return;
        }

        var equippedItems = new List<UOItemEntity>();

        foreach (var itemId in mobile.EquippedItemIds.Values)
        {
            if (itemId == Serial.Zero)
            {
                continue;
            }

            var equippedItem = await _itemService.GetItemAsync(itemId);

            if (equippedItem is null)
            {
                continue;
            }

            equippedItems.Add(equippedItem);
        }

        mobile.HydrateEquipmentRuntime(equippedItems);

        var sector = _spatialWorldService.GetSectorByLocation(mobile.MapId, mobile.Location);

        var sessionIdsToNotify = new HashSet<long>();

        if (_gameNetworkSessionService.TryGetByCharacterId(characterId, out var sourceSession))
        {
            sessionIdsToNotify.Add(sourceSession.SessionId);
        }

        if (sector is null)
        {
            foreach (var sessionId in sessionIdsToNotify)
            {
                EnqueueVisibleWornItemsForSession(sessionId, mobile);
            }

            return;
        }

        var players = _spatialWorldService.GetPlayersInSector(mobile.MapId, sector.SectorX, sector.SectorY);

        foreach (var player in players)
        {
            if (_gameNetworkSessionService.TryGetByCharacterId(player.Id, out var session))
            {
                sessionIdsToNotify.Add(session.SessionId);
            }
        }

        foreach (var sessionId in sessionIdsToNotify)
        {
            EnqueueVisibleWornItemsForSession(sessionId, mobile);
        }
    }

    private static bool IsValidWearLayer(ItemLayerType layer)
    {
        if (layer < ItemLayerType.FirstValid || layer > ItemLayerType.LastUserValid)
        {
            return false;
        }

        return layer is not ItemLayerType.Backpack and not ItemLayerType.Bank;
    }

    private void EnqueueVisibleWornItemsForSession(long sessionId, UOMobileEntity mobile)
        => WornItemPacketHelper.EnqueueVisibleWornItems(mobile, packet => Enqueue(sessionId, packet));
}
