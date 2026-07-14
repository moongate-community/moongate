using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Interfaces.Items;
using Moongate.Ultima.Types;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Services.Items;

/// <summary>Default <see cref="IItemService" />: the single writer of item persistence and item-to-mobile links.</summary>
public sealed class ItemService : IItemService
{
    private readonly IEntityStore<ItemEntity, Serial> _items;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;

    public ItemService(IPersistenceService persistenceService)
    {
        _items = persistenceService.GetStore<ItemEntity, Serial>();
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
    }

    public void AddToContainer(ItemEntity container, ItemEntity item, Point2D position)
    {
        if (container.Id == Serial.Zero)
        {
            _items.UpsertAsync(container).WaitSync();
        }

        DetachFromCurrentLocation(item);

        item.ParentContainerId = container.Id;
        item.ContainerPosition = position;
        _items.UpsertAsync(item).WaitSync();

        if (!container.ContainedItemIds.Contains(item.Id))
        {
            container.ContainedItemIds.Add(item.Id);
        }

        _items.UpsertAsync(container).WaitSync();
    }

    public Serial Create(ItemEntity item)
    {
        _items.UpsertAsync(item).WaitSync();

        return item.Id;
    }

    public bool Delete(Serial itemId)
        => _items.RemoveAsync(itemId).WaitSync();

    public void Equip(MobileEntity mobile, ItemEntity item, LayerType layer)
    {
        if (mobile.Id == Serial.Zero)
        {
            _mobiles.UpsertAsync(mobile).WaitSync();
        }

        DetachFromCurrentLocation(item);

        item.EquippedMobileId = mobile.Id;
        item.EquippedLayer = layer;
        _items.UpsertAsync(item).WaitSync();

        mobile.EquippedItemIds[layer] = item.Id;

        if (layer == LayerType.Backpack)
        {
            mobile.BackpackId = item.Id;
        }

        _mobiles.UpsertAsync(mobile).WaitSync();
    }

    public ItemEntity? GetById(Serial itemId)
        => _items.GetById(itemId);

    public IReadOnlyList<ItemEntity> GetContents(Serial containerId)
    {
        var container = _items.GetById(containerId);

        return container is null ? [] : Resolve(container.ContainedItemIds);
    }

    public IReadOnlyList<ItemEntity> GetEquipped(MobileEntity mobile)
        => Resolve(mobile.EquippedItemIds.Values);

    public void RemoveFromContainer(ItemEntity container, ItemEntity item)
    {
        container.ContainedItemIds.Remove(item.Id);
        _items.UpsertAsync(container).WaitSync();

        item.ParentContainerId = Serial.Zero;
        item.ContainerPosition = Point2D.Zero;
        _items.UpsertAsync(item).WaitSync();
    }

    public void Save(ItemEntity item)
        => _items.UpsertAsync(item).WaitSync();

    public ItemEntity? Unequip(MobileEntity mobile, LayerType layer)
    {
        if (!mobile.EquippedItemIds.Remove(layer, out var itemId))
        {
            return null;
        }

        if (layer == LayerType.Backpack)
        {
            mobile.BackpackId = Serial.Zero;
        }

        _mobiles.UpsertAsync(mobile).WaitSync();

        var item = _items.GetById(itemId);

        if (item is not null)
        {
            item.EquippedMobileId = Serial.Zero;
            item.EquippedLayer = null;
            _items.UpsertAsync(item).WaitSync();
        }

        return item;
    }

    private void DetachFromCurrentLocation(ItemEntity item)
    {
        if (item.ParentContainerId != Serial.Zero)
        {
            var container = _items.GetById(item.ParentContainerId);

            if (container is not null && container.ContainedItemIds.Remove(item.Id))
            {
                _items.UpsertAsync(container).WaitSync();
            }

            item.ParentContainerId = Serial.Zero;
            item.ContainerPosition = Point2D.Zero;
        }

        if (item.EquippedMobileId != Serial.Zero)
        {
            var owner = _mobiles.GetById(item.EquippedMobileId);

            if (owner is not null && item.EquippedLayer is { } oldLayer && owner.EquippedItemIds.Remove(oldLayer))
            {
                if (oldLayer == LayerType.Backpack)
                {
                    owner.BackpackId = Serial.Zero;
                }

                _mobiles.UpsertAsync(owner).WaitSync();
            }

            item.EquippedMobileId = Serial.Zero;
            item.EquippedLayer = null;
        }
    }

    private IReadOnlyList<ItemEntity> Resolve(IEnumerable<Serial> ids)
    {
        var result = new List<ItemEntity>();

        foreach (var id in ids)
        {
            var item = _items.GetById(id);

            if (item is not null)
            {
                result.Add(item);
            }
        }

        return result;
    }
}
