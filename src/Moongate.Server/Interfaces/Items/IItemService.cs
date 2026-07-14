using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Ultima.Types;

namespace Moongate.Server.Interfaces.Items;

/// <summary>Owns runtime item persistence and the bidirectional item-to-mobile links (equip, containment, resolve).</summary>
public interface IItemService
{
    /// <summary>Places an item inside a container at a position, detaching it from any previous location.</summary>
    void AddToContainer(ItemEntity container, ItemEntity item, Point2D position);

    /// <summary>Persists a new item, allocating and returning its serial.</summary>
    Serial Create(ItemEntity item);

    /// <summary>Removes an item from the store; true when it existed.</summary>
    bool Delete(Serial itemId);

    /// <summary>Equips an item onto a mobile at a layer, detaching it from any previous location.</summary>
    void Equip(MobileEntity mobile, ItemEntity item, LayerType layer);

    /// <summary>Returns the item with the given serial, or null.</summary>
    ItemEntity? GetById(Serial itemId);

    /// <summary>Resolves the items contained in the given container.</summary>
    IReadOnlyList<ItemEntity> GetContents(Serial containerId);

    /// <summary>Resolves the items equipped on the given mobile.</summary>
    IReadOnlyList<ItemEntity> GetEquipped(MobileEntity mobile);

    /// <summary>Removes an item from a container, clearing both sides.</summary>
    void RemoveFromContainer(ItemEntity container, ItemEntity item);

    /// <summary>Persists changes to an existing item.</summary>
    void Save(ItemEntity item);

    /// <summary>Unequips the item on a layer, clearing both sides; returns the detached item, or null.</summary>
    ItemEntity? Unequip(MobileEntity mobile, LayerType layer);
}
