using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Items;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using Serilog;

namespace Moongate.Server.Services.Items;

/// <summary>
/// Provides persistence-backed operations for creating, moving and equipping items.
/// </summary>
public sealed class ItemService : IItemService
{
    private readonly ILogger _logger = Log.ForContext<ItemService>();
    private readonly IPersistenceService _persistenceService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IItemFactoryService? _itemFactoryService;
    private readonly IMobileModifierAggregationService? _mobileModifierAggregationService;

    public ItemService(
        IPersistenceService persistenceService,
        IGameEventBusService gameEventBusService,
        IItemFactoryService? itemFactoryService = null,
        IMobileModifierAggregationService? mobileModifierAggregationService = null
    )
    {
        _persistenceService = persistenceService;
        _gameEventBusService = gameEventBusService;
        _itemFactoryService = itemFactoryService;
        _mobileModifierAggregationService = mobileModifierAggregationService;
    }

    public async Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            if (item.Id == Serial.Zero)
            {
                item.Id = _persistenceService.UnitOfWork.AllocateNextItemId();
            }
        }

        await _persistenceService.UnitOfWork.Items.BulkUpsertAsync(items);
    }

    public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
    {
        ArgumentNullException.ThrowIfNull(item);

        var clone = new UOItemEntity
        {
            Id = generateNewSerial ? _persistenceService.UnitOfWork.AllocateNextItemId() : item.Id,
            Location = item.Location,
            MapId = item.MapId,
            Name = item.Name,
            Weight = item.Weight,
            Amount = item.Amount,
            ItemId = item.ItemId,
            Hue = item.Hue,
            GumpId = item.GumpId,
            Direction = item.Direction,
            ScriptId = item.ScriptId,
            IsStackable = item.IsStackable,
            Rarity = item.Rarity,
            Visibility = item.Visibility,
            ParentContainerId = item.ParentContainerId,
            ContainerPosition = item.ContainerPosition,
            EquippedMobileId = item.EquippedMobileId,
            EquippedLayer = item.EquippedLayer,
            ContainedItemIds = [.. item.ContainedItemIds]
        };

        return clone;
    }

    public async Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
    {
        var item = await _persistenceService.UnitOfWork.Items.GetByIdAsync(itemId);

        if (item is null)
        {
            return null;
        }

        return Clone(item, generateNewSerial);
    }

    public async Task<Serial> CreateItemAsync(UOItemEntity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Id == Serial.Zero)
        {
            item.Id = _persistenceService.UnitOfWork.AllocateNextItemId();
        }

        await _persistenceService.UnitOfWork.Items.UpsertAsync(item);
        _logger.Verbose("Created item {ItemId} (ItemId=0x{TileId:X4})", item.Id, item.ItemId);

        return item.Id;
    }

    public async Task<bool> DeleteItemAsync(Serial itemId)
    {
        var item = await _persistenceService.UnitOfWork.Items.GetByIdAsync(itemId);

        if (item is null)
        {
            _logger.Warning("Cannot delete item {ItemId}: not found", itemId);

            return false;
        }

        var oldContainerId = item.ParentContainerId;
        var oldLocation = item.Location;
        var mapId = item.MapId;

        await DetachFromCurrentOwnerAsync(item);
        var removed = await _persistenceService.UnitOfWork.Items.RemoveAsync(itemId);

        if (removed)
        {
            await _gameEventBusService.PublishAsync(
                new ItemDeletedEvent(
                    0,
                    itemId,
                    oldContainerId,
                    oldLocation,
                    mapId
                )
            );
        }

        _logger.Debug("Deleted item {ItemId} Removed={Removed}", itemId, removed);

        return removed;
    }

    public async Task<DropItemToGroundResult?> DropItemToGroundAsync(
        Serial itemId,
        Point3D location,
        int mapId,
        long sessionId = 0
    )
    {
        var item = await _persistenceService.UnitOfWork.Items.GetByIdAsync(itemId);

        if (item is null)
        {
            _logger.Warning("Cannot drop item {ItemId} to ground: item not found", itemId);

            return null;
        }

        var sourceContainerId = item.ParentContainerId;
        var oldLocation = item.Location;
        var moved = await MoveItemToWorldAsync(itemId, location, mapId, sessionId);

        if (!moved)
        {
            return null;
        }

        return new(itemId, sourceContainerId, oldLocation, location);
    }

    public async Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
    {
        var item = await _persistenceService.UnitOfWork.Items.GetByIdAsync(itemId);

        if (item is null)
        {
            _logger.Warning("Cannot equip item {ItemId}: item not found", itemId);

            return false;
        }

        var mobile = await _persistenceService.UnitOfWork.Mobiles.GetByIdAsync(mobileId);

        if (mobile is null)
        {
            _logger.Warning("Cannot equip item {ItemId}: mobile {MobileId} not found", itemId, mobileId);

            return false;
        }

        await DetachFromCurrentOwnerAsync(item);
        await TryUnequipCurrentLayerItemAsync(mobile, layer);

        item.ParentContainerId = Serial.Zero;
        item.ContainerPosition = Point2D.Zero;
        item.MapId = mobile.MapId;
        mobile.AddEquippedItem(layer, item);

        if (layer == ItemLayerType.Backpack)
        {
            mobile.BackpackId = itemId;
        }

        RecalculateEquipmentModifiers(mobile);
        await _persistenceService.UnitOfWork.Items.UpsertAsync(item);
        await _persistenceService.UnitOfWork.Mobiles.UpsertAsync(mobile);
        await _gameEventBusService.PublishAsync(new ItemEquippedEvent(itemId, mobileId, layer));

        _logger.Debug("Equipped item {ItemId} on mobile {MobileId} at layer {Layer}", itemId, mobileId, layer);

        return true;
    }

    public async Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
    {
        var minX = sectorX * MapSectorConsts.SectorSize;
        var maxX = minX + MapSectorConsts.SectorSize;
        var minY = sectorY * MapSectorConsts.SectorSize;
        var maxY = minY + MapSectorConsts.SectorSize;

        var items = await _persistenceService.UnitOfWork.Items.QueryAsync(
                        item =>
                            item.MapId == mapId &&
                            item.ParentContainerId == Serial.Zero &&
                            item.EquippedMobileId == Serial.Zero &&
                            item.Location.X >= minX &&
                            item.Location.X < maxX &&
                            item.Location.Y >= minY &&
                            item.Location.Y < maxY,
                        static item => item
                    );

        return [.. items];
    }

    public Task<UOItemEntity?> GetItemAsync(Serial itemId)
        => GetItemHydratedAsync(itemId);

    public async Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
    {
        var items = await _persistenceService.UnitOfWork.Items.QueryAsync(
                        item => item.ParentContainerId == containerId,
                        static item => item
                    );

        return [.. items];
    }

    public async Task<bool> MoveItemToContainerAsync(
        Serial itemId,
        Serial containerId,
        Point2D position,
        long sessionId = 0
    )
    {
        var item = await _persistenceService.UnitOfWork.Items.GetByIdAsync(itemId);

        if (item is null)
        {
            _logger.Warning("Cannot move item {ItemId} to container {ContainerId}: item not found", itemId, containerId);

            return false;
        }

        var container = await GetItemHydratedAsync(containerId);

        if (container is null)
        {
            _logger.Warning(
                "Cannot move item {ItemId} to container {ContainerId}: container not found",
                itemId,
                containerId
            );

            return false;
        }

        var oldContainerId = item.ParentContainerId;
        var oldLocation = item.Location;

        await DetachFromCurrentOwnerAsync(item);
        container.AddItem(item, position);
        item.MapId = container.MapId;

        await _persistenceService.UnitOfWork.Items.UpsertAsync(item);
        await _persistenceService.UnitOfWork.Items.UpsertAsync(container);
        await PublishItemMovedEventAsync(
            sessionId,
            itemId,
            oldContainerId,
            containerId,
            oldLocation,
            item.Location,
            item.MapId
        );
        _logger.Debug(
            "Moved item {ItemId} to container {ContainerId} at {X},{Y}",
            itemId,
            containerId,
            position.X,
            position.Y
        );

        return true;
    }

    public async Task<bool> MoveItemToWorldAsync(
        Serial itemId,
        Point3D location,
        int mapId,
        long sessionId = 0
    )
    {
        var item = await _persistenceService.UnitOfWork.Items.GetByIdAsync(itemId);

        if (item is null)
        {
            _logger.Warning("Cannot move item {ItemId} to world: item not found", itemId);

            return false;
        }

        var oldContainerId = item.ParentContainerId;
        var oldLocation = item.Location;

        await DetachFromCurrentOwnerAsync(item);

        item.Location = location;
        item.MapId = mapId;
        item.ParentContainerId = Serial.Zero;
        item.ContainerPosition = Point2D.Zero;
        item.EquippedMobileId = Serial.Zero;
        item.EquippedLayer = null;

        await _persistenceService.UnitOfWork.Items.UpsertAsync(item);
        await PublishItemMovedEventAsync(sessionId, itemId, oldContainerId, Serial.Zero, oldLocation, location, mapId);
        _logger.Debug("Moved item {ItemId} to world location {Location}", itemId, location);

        return true;
    }

    public async Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemTemplateId);

        if (_itemFactoryService is null)
        {
            throw new InvalidOperationException("Item factory service is not configured for ItemService.");
        }

        var item = _itemFactoryService.CreateItemFromTemplate(itemTemplateId);
        await _persistenceService.UnitOfWork.Items.UpsertAsync(item);
        _logger.Verbose(
            "Spawned item {ItemId} from template {TemplateId} (ItemId=0x{TileId:X4})",
            item.Id,
            itemTemplateId,
            item.ItemId
        );

        return item;
    }

    public async Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
    {
        var item = await GetItemHydratedAsync(itemId);

        return (item is not null, item);
    }

    public async Task UpsertItemAsync(UOItemEntity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Id == Serial.Zero)
        {
            item.Id = _persistenceService.UnitOfWork.AllocateNextItemId();
        }
        await _persistenceService.UnitOfWork.Items.UpsertAsync(item);
    }

    public async Task UpsertItemsAsync(params UOItemEntity[] items)
    {
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            await UpsertItemAsync(item);
        }
    }

    private async Task DetachFromCurrentOwnerAsync(UOItemEntity item)
    {
        if (item.ParentContainerId != Serial.Zero)
        {
            var parentContainer = await GetItemHydratedAsync(item.ParentContainerId);

            if (parentContainer is not null)
            {
                parentContainer.RemoveItem(item.Id);
                await _persistenceService.UnitOfWork.Items.UpsertAsync(parentContainer);
            }

            item.ParentContainerId = Serial.Zero;
            item.ContainerPosition = Point2D.Zero;
        }

        if (item.EquippedMobileId == Serial.Zero || item.EquippedLayer is null)
        {
            return;
        }

        var mobile = await _persistenceService.UnitOfWork.Mobiles.GetByIdAsync(item.EquippedMobileId);

        if (mobile is null)
        {
            item.EquippedMobileId = Serial.Zero;
            item.EquippedLayer = null;

            return;
        }

        var layer = item.EquippedLayer.Value;

        if (mobile.EquippedItemIds.TryGetValue(layer, out var equippedItemId) && equippedItemId == item.Id)
        {
            mobile.UnequipItem(layer, item);
            await _persistenceService.UnitOfWork.Mobiles.UpsertAsync(mobile);
        }

        item.EquippedMobileId = Serial.Zero;
        item.EquippedLayer = null;
        RecalculateEquipmentModifiers(mobile);
        await _persistenceService.UnitOfWork.Mobiles.UpsertAsync(mobile);
    }

    private async Task<UOItemEntity?> GetItemHydratedAsync(Serial itemId)
    {
        var visited = new HashSet<Serial>();

        return await GetItemHydratedRecursiveAsync(itemId, visited);
    }

    private async Task<UOItemEntity?> GetItemHydratedRecursiveAsync(Serial itemId, HashSet<Serial> visited)
    {
        if (!visited.Add(itemId))
        {
            return null;
        }

        var item = await _persistenceService.UnitOfWork.Items.GetByIdAsync(itemId);

        if (item is null)
        {
            return null;
        }

        List<Serial> childIds;

        if (item.ContainedItemIds.Count > 0)
        {
            childIds = [.. item.ContainedItemIds];
        }
        else
        {
            var queriedChildIds = await _persistenceService.UnitOfWork.Items.QueryAsync(
                                      i => i.ParentContainerId == item.Id,
                                      static i => i.Id
                                  );
            childIds = [.. queriedChildIds];
        }

        if (childIds.Count == 0)
        {
            item.HydrateContainedItemsRuntime([]);

            return item;
        }

        var containedItems = new List<UOItemEntity>(childIds.Count);

        foreach (var childId in childIds)
        {
            var child = await GetItemHydratedRecursiveAsync(childId, visited);

            if (child is null || child.ParentContainerId != item.Id)
            {
                continue;
            }

            containedItems.Add(child);
        }

        item.HydrateContainedItemsRuntime(containedItems);

        return item;
    }

    private ValueTask PublishItemMovedEventAsync(
        long sessionId,
        Serial itemId,
        Serial oldContainerId,
        Serial newContainerId,
        Point3D oldLocation,
        Point3D newLocation,
        int mapId
    )
        => _gameEventBusService.PublishAsync(
            new ItemMovedEvent(sessionId, itemId, oldContainerId, newContainerId, oldLocation, newLocation, mapId)
        );

    private void RecalculateEquipmentModifiers(UOMobileEntity mobile)
        => _mobileModifierAggregationService?.RecalculateEquipmentModifiers(mobile);

    private async Task TryUnequipCurrentLayerItemAsync(UOMobileEntity mobile, ItemLayerType layer)
    {
        if (!mobile.EquippedItemIds.TryGetValue(layer, out var currentItemId) || currentItemId == Serial.Zero)
        {
            return;
        }

        var currentItem = await _persistenceService.UnitOfWork.Items.GetByIdAsync(currentItemId);

        if (currentItem is not null)
        {
            mobile.UnequipItem(layer, currentItem);
            await _persistenceService.UnitOfWork.Items.UpsertAsync(currentItem);
        }
        else
        {
            mobile.UnequipItem(layer);
        }
    }
}
