using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Entities;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

/// <summary>
/// Minimal item entity implementation used by map and container systems.
/// </summary>
public class UOItemEntity : IItemEntity
{
    private readonly List<UOItemEntity> _items = new();
    private readonly Dictionary<Serial, ItemReference> _containedItemReferences = [];

    public Serial Id { get; set; }

    public Point3D Location { get; set; }

    /// <summary>
    /// Gets or sets world map id for items placed in world or equipped by mobiles.
    /// </summary>
    public int MapId { get; set; }

    public string? Name { get; set; }

    public int Weight { get; set; }

    public int Amount { get; set; } = 1;

    public int ItemId { get; set; }

    public int Hue { get; set; }

    public int? GumpId { get; set; }

    public bool IsStackable { get; set; }

    public string ScriptId { get; set; }

    public ItemRarity Rarity { get; set; }

    /// <summary>
    /// Gets or sets parent container serial when the item is inside a container.
    /// </summary>
    public Serial ParentContainerId { get; set; }

    /// <summary>
    /// Gets or sets item position inside the parent container.
    /// </summary>
    public Point2D ContainerPosition { get; set; }

    /// <summary>
    /// Gets or sets the mobile serial when the item is equipped.
    /// </summary>
    public Serial EquippedMobileId { get; set; }

    /// <summary>
    /// Gets or sets the equipped layer when the item is worn.
    /// </summary>
    public ItemLayerType? EquippedLayer { get; set; }

    /// <summary>
    /// Gets container child items when this item acts as a container.
    /// </summary>
    public IReadOnlyList<UOItemEntity> Items => _items;

    /// <summary>
    /// Gets or sets persisted contained item serial identifiers.
    /// </summary>
    public List<Serial> ContainedItemIds { get; set; } = [];

    /// <summary>
    /// Gets runtime contained-item snapshots keyed by child serial.
    /// This cache is not used for persistence.
    /// </summary>
    public IReadOnlyDictionary<Serial, ItemReference> ContainedItemReferences => _containedItemReferences;

    public bool IsContainer => TileData.ItemTable[ItemId][UOTileFlag.Container];

    public void AddItem(IItemEntity item, Point2D position)
    {
        if (item is UOItemEntity typedItem)
        {
            typedItem.ParentContainerId = Id;
            typedItem.ContainerPosition = position;
            typedItem.Location = new(position.X, position.Y, 0);
            typedItem.EquippedMobileId = Serial.Zero;
            typedItem.EquippedLayer = null;
            _containedItemReferences[typedItem.Id] = new(typedItem.Id, typedItem.ItemId, typedItem.Hue);

            if (!ContainedItemIds.Contains(typedItem.Id))
            {
                ContainedItemIds.Add(typedItem.Id);
            }

            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i].Id != typedItem.Id)
                {
                    continue;
                }

                _items[i] = typedItem;

                return;
            }

            _items.Add(typedItem);
        }
    }

    /// <summary>
    /// Hydrates runtime contained-item references and in-memory child list from resolved entities.
    /// </summary>
    /// <param name="containedItems">Resolved contained items for this container.</param>
    public void HydrateContainedItemsRuntime(IEnumerable<UOItemEntity> containedItems)
    {
        ArgumentNullException.ThrowIfNull(containedItems);

        _items.Clear();
        _containedItemReferences.Clear();
        ContainedItemIds.Clear();

        foreach (var item in containedItems)
        {
            if (item.ParentContainerId != Id)
            {
                continue;
            }

            ContainedItemIds.Add(item.Id);
            _containedItemReferences[item.Id] = new(item.Id, item.ItemId, item.Hue);
            item.Location = new(item.ContainerPosition.X, item.ContainerPosition.Y, 0);
            _items.Add(item);
        }
    }

    /// <summary>
    /// Removes a contained item entry from this container.
    /// </summary>
    /// <param name="itemId">Contained item serial identifier.</param>
    /// <returns><c>true</c> when removed; otherwise <c>false</c>.</returns>
    public bool RemoveItem(Serial itemId)
    {
        var removed = false;

        for (var i = _items.Count - 1; i >= 0; i--)
        {
            if (_items[i].Id != itemId)
            {
                continue;
            }

            _items.RemoveAt(i);
            removed = true;
        }

        if (ContainedItemIds.Remove(itemId))
        {
            removed = true;
        }

        _containedItemReferences.Remove(itemId);

        return removed;
    }

    public override string ToString()
        => $"Item(Id={Id}, Name={Name}, ItemId=0x{ItemId:X4}, MapId={MapId}, Location={Location})";

    /// <summary>
    /// Updates the container-local position for an item contained in this container.
    /// </summary>
    /// <param name="item">Contained item to update.</param>
    /// <param name="position">New container-local position.</param>
    /// <returns><c>true</c> when the item is contained and updated; otherwise <c>false</c>.</returns>
    public bool UpdateItemLocation(UOItemEntity item, Point2D position)
    {
        ArgumentNullException.ThrowIfNull(item);

        for (var i = 0; i < _items.Count; i++)
        {
            if (_items[i].Id != item.Id)
            {
                continue;
            }

            item.ParentContainerId = Id;
            item.ContainerPosition = position;
            item.Location = new(position.X, position.Y, 0);
            item.EquippedMobileId = Serial.Zero;
            item.EquippedLayer = null;
            _containedItemReferences[item.Id] = new(item.Id, item.ItemId, item.Hue);
            _items[i] = item;

            return true;
        }

        return false;
    }
}
