using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Internal.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Items;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Services.Items;

public class ItemManipulationService : IItemManipulationService
{
    private readonly ILogger _logger = Log.ForContext<ItemManipulationService>();
    private readonly IItemService _itemService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IPlayerDragService _playerDragService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IMobileService _mobileService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    public ItemManipulationService(
        IItemService itemService,
        IGameEventBusService gameEventBusService,
        IPlayerDragService playerDragService,
        ISpatialWorldService spatialWorldService,
        IMobileService mobileService,
        IGameNetworkSessionService gameNetworkSessionService,
        IOutgoingPacketQueue outgoingPacketQueue
    )
    {
        _itemService = itemService;
        _gameEventBusService = gameEventBusService;
        _playerDragService = playerDragService;
        _spatialWorldService = spatialWorldService;
        _mobileService = mobileService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _outgoingPacketQueue = outgoingPacketQueue;
    }

    public async Task<bool> HandleDropItemAsync(
        GameSession session,
        DropItemPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        if (!_playerDragService.TryGet(session.SessionId, out var dragState) ||
            dragState.ItemId != packet.ItemSerial)
        {
            _logger.Warning(
                "Drop rejected Session={SessionId} ItemId={ItemId}: no matching pending drag state",
                session.SessionId,
                packet.ItemSerial
            );

            return false;
        }

        if (!packet.IsGroundDrop)
        {
            await DropItemInContainerAsync(session, packet);
            _playerDragService.Clear(session.SessionId);

            return true;
        }

        await DropItemOnGroundAsync(session, packet);
        _playerDragService.Clear(session.SessionId);

        return true;
    }

    public async Task<bool> HandleDropWearItemAsync(
        GameSession session,
        DropWearItemPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        if (session.Character is null || session.CharacterId == Serial.Zero)
        {
            return false;
        }

        if (packet.PlayerSerial != session.CharacterId)
        {
            _logger.Warning(
                "DropWear rejected Session={SessionId} ItemId={ItemId}: target player mismatch packet={PacketPlayerId} session={SessionPlayerId}",
                session.SessionId,
                packet.ItemSerial,
                packet.PlayerSerial,
                session.CharacterId
            );

            return false;
        }

        if (!IsValidWearLayer(packet.Layer))
        {
            _logger.Warning(
                "DropWear rejected Session={SessionId} ItemId={ItemId}: invalid requested layer {Layer}",
                session.SessionId,
                packet.ItemSerial,
                packet.Layer
            );

            return false;
        }

        await _itemService.EquipItemAsync(
            packet.ItemSerial,
            session.CharacterId,
            packet.Layer
        );

        await DispatchItemWearChange(session.CharacterId);

        return true;
    }

    public async Task<bool> HandlePickUpItemAsync(
        GameSession session,
        PickUpItemPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;
        var item = await _itemService.GetItemAsync(packet.ItemSerial);

        if (item is null)
        {
            return false;
        }

        var requestedAmount = Math.Max(1, packet.StackAmount);
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

            item.Amount = pickedAmount;
            item.ParentContainerId = Serial.Zero;
            item.ContainerPosition = Point2D.Zero;
            item.Location = sourceLocation;

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

        var updateRadius = _spatialWorldService.GetUpdateBroadcastSectorRadius();

        for (var sectorX = sector.SectorX - updateRadius;
             sectorX <= sector.SectorX + updateRadius;
             sectorX++)
        {
            for (var sectorY = sector.SectorY - updateRadius;
                 sectorY <= sector.SectorY + updateRadius;
                 sectorY++)
            {
                var players = _spatialWorldService.GetPlayersInSector(mobile.MapId, sectorX, sectorY);

                foreach (var player in players)
                {
                    if (_gameNetworkSessionService.TryGetByCharacterId(player.Id, out var session))
                    {
                        sessionIdsToNotify.Add(session.SessionId);
                    }
                }
            }
        }

        foreach (var sessionId in sessionIdsToNotify)
        {
            EnqueueVisibleWornItemsForSession(sessionId, mobile);
        }
    }

    private async Task DropItemInContainerAsync(GameSession session, DropItemPacket packet)
    {
        var item = await _itemService.GetItemAsync(packet.ItemSerial);

        if (item is null)
        {
            return;
        }

        var destinationContainer = await _itemService.GetItemAsync(packet.DestinationSerial);

        if (destinationContainer is null)
        {
            return;
        }

        var containerToRefreshId = destinationContainer.Id;

        if (!destinationContainer.IsContainer &&
            destinationContainer.IsStackable &&
            destinationContainer.ItemId == item.ItemId)
        {
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
                new(packet.Location.X, packet.Location.Y),
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

    private async Task DropItemOnGroundAsync(GameSession session, DropItemPacket packet)
    {
        var mapId = session.Character?.MapId ?? 0;
        var dropResult = await _itemService.DropItemToGroundAsync(
                             packet.ItemSerial,
                             packet.Location,
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

    private void Enqueue(GameSession session, IGameNetworkPacket packet)
        => _outgoingPacketQueue.Enqueue(session.SessionId, packet);

    private void EnqueueVisibleWornItemsForSession(long sessionId, UOMobileEntity mobile)
        => WornItemPacketHelper.EnqueueVisibleWornItems(mobile, packet => _outgoingPacketQueue.Enqueue(sessionId, packet));

    private static bool IsValidWearLayer(ItemLayerType layer)
    {
        if (layer < ItemLayerType.FirstValid || layer > ItemLayerType.LastUserValid)
        {
            return false;
        }

        return layer is not ItemLayerType.Backpack and not ItemLayerType.Bank;
    }
}
