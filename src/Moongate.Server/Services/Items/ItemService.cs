using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Ultima.Types;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Services.Items;

/// <summary>Default <see cref="IItemService" />: the single writer of item persistence and item-to-mobile links.</summary>
public sealed class ItemService : IItemService
{
    private readonly IEntityStore<ItemEntity, Serial> _items;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly IOplService? _opl;
    private readonly ISpatialIndexService? _spatial;
    private readonly ILoopAffinity? _loopAffinity;

    // The property-list cache and the spatial index are optional on purpose: tests build a bare
    // ItemService, and both are concerns the item flows only need to keep in sync, not require.
    public ItemService(
        IPersistenceService persistenceService,
        IOplService? opl = null,
        ISpatialIndexService? spatial = null,
        ILoopAffinity? loopAffinity = null
    )
    {
        _items = persistenceService.GetStore<ItemEntity, Serial>();
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
        _opl = opl;
        _spatial = spatial;
        _loopAffinity = loopAffinity;
    }

    public void AddToContainer(ItemEntity container, ItemEntity item, Point2D position)
    {
        _loopAffinity?.AssertOnLoop("item.add_to_container");

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
        _spatial?.AddOrUpdate(item);
    }

    public Serial Create(ItemEntity item)
    {
        _loopAffinity?.AssertOnLoop("item.create");
        _items.UpsertAsync(item).WaitSync();
        _spatial?.AddOrUpdate(item);

        return item.Id;
    }

    public bool Delete(Serial itemId)
    {
        _loopAffinity?.AssertOnLoop("item.delete");
        _opl?.Invalidate(itemId);
        _spatial?.Remove(itemId);

        return _items.RemoveAsync(itemId).WaitSync();
    }

    public void Equip(MobileEntity mobile, ItemEntity item, LayerType layer)
    {
        _loopAffinity?.AssertOnLoop("item.equip");

        if (mobile.Id == Serial.Zero)
        {
            _mobiles.UpsertAsync(mobile).WaitSync();
        }

        DetachFromCurrentLocation(item);

        item.EquippedMobileId = mobile.Id;
        item.EquippedLayer = layer;
        _items.UpsertAsync(item).WaitSync();
        _spatial?.AddOrUpdate(item);

        mobile.EquippedItemIds[layer] = item.Id;

        if (layer == LayerType.Backpack)
        {
            mobile.BackpackId = item.Id;
        }

        _mobiles.UpsertAsync(mobile).WaitSync();
    }

    public bool Flip(ItemEntity item)
    {
        _loopAffinity?.AssertOnLoop("item.flip");

        if (item.FlippableItemIds.Count < 2)
        {
            return false;
        }

        var index = item.FlippableItemIds.IndexOf(item.ItemId);

        if (index < 0)
        {
            return false;
        }

        item.ItemId = item.FlippableItemIds[(index + 1) % item.FlippableItemIds.Count];
        _items.UpsertAsync(item).WaitSync();

        return true;
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
        _loopAffinity?.AssertOnLoop("item.remove_from_container");
        container.ContainedItemIds.Remove(item.Id);
        _items.UpsertAsync(container).WaitSync();

        item.ParentContainerId = Serial.Zero;
        item.ContainerPosition = Point2D.Zero;
        _items.UpsertAsync(item).WaitSync();
        _spatial?.AddOrUpdate(item);
    }

    public void Save(ItemEntity item)
    {
        _loopAffinity?.AssertOnLoop("item.save");
        _items.UpsertAsync(item).WaitSync();
        _opl?.Invalidate(item.Id);
        _spatial?.AddOrUpdate(item);
    }

    public ItemEntity? Unequip(MobileEntity mobile, LayerType layer)
    {
        _loopAffinity?.AssertOnLoop("item.unequip");

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
            _spatial?.AddOrUpdate(item);
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
