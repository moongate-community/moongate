using MemoryPack;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Entities;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

/// <summary>
/// Minimal item entity implementation used by map and container systems.
/// </summary>
[MemoryPackable(SerializeLayout.Explicit)]
public partial class UOItemEntity : IItemEntity
{
    private readonly List<UOItemEntity> _items = new();
    private readonly Dictionary<Serial, ItemReference> _containedItemReferences = [];

    [MemoryPackInclude]
    [MemoryPackOrder(21)]
    private Dictionary<string, ItemCustomProperty> _customProperties = new(StringComparer.OrdinalIgnoreCase);
    [MemoryPackOrder(0)]
    public Serial Id { get; set; }

    [MemoryPackOrder(1)]
    public Point3D Location { get; set; }

    /// <summary>
    /// Gets or sets world map id for items placed in world or equipped by mobiles.
    /// </summary>
    [MemoryPackOrder(2)]
    public int MapId { get; set; }
    [MemoryPackOrder(3)]
    public string? Name { get; set; }
    [MemoryPackOrder(4)]
    public int Weight { get; set; }

    [MemoryPackOrder(5)]
    public int Amount { get; set; } = 1;
    [MemoryPackOrder(6)]
    public int ItemId { get; set; }
    [MemoryPackOrder(7)]
    public int Hue { get; set; }
    [MemoryPackOrder(8)]
    public int? GumpId { get; set; }

    /// <summary>
    /// Gets or sets item world-facing direction used by item packets that expose orientation.
    /// </summary>
    [MemoryPackOrder(9)]
    public DirectionType Direction { get; set; }
    [MemoryPackOrder(10)]
    public bool IsStackable { get; set; }

    [MemoryPackIgnore]
    public bool IsDoor => TileData.ItemTable[ItemId][UOTileFlag.Door];
    [MemoryPackOrder(11)]
    public string ScriptId { get; set; }
    [MemoryPackOrder(12)]
    public ItemRarity Rarity { get; set; }
    [MemoryPackOrder(13)]
    public AccountType Visibility { get; set; } = AccountType.Regular;
    [MemoryPackOrder(14)]
    public ItemCombatStats? CombatStats { get; set; }
    [MemoryPackOrder(15)]
    public ItemModifiers? Modifiers { get; set; }

    /// <summary>
    /// Gets or sets parent container serial when the item is inside a container.
    /// </summary>
    [MemoryPackOrder(16)]
    public Serial ParentContainerId { get; set; }

    /// <summary>
    /// Gets or sets item position inside the parent container.
    /// </summary>
    [MemoryPackOrder(17)]
    public Point2D ContainerPosition { get; set; }

    /// <summary>
    /// Gets or sets the mobile serial when the item is equipped.
    /// </summary>
    [MemoryPackOrder(18)]
    public Serial EquippedMobileId { get; set; }

    /// <summary>
    /// Gets or sets the equipped layer when the item is worn.
    /// </summary>
    [MemoryPackOrder(19)]
    public ItemLayerType? EquippedLayer { get; set; }

    /// <summary>
    /// Gets container child items when this item acts as a container.
    /// </summary>
    [MemoryPackIgnore]
    public IReadOnlyList<UOItemEntity> Items => _items;

    /// <summary>
    /// Gets or sets persisted contained item serial identifiers.
    /// </summary>
    [MemoryPackOrder(20)]
    public List<Serial> ContainedItemIds { get; set; } = [];

    /// <summary>
    /// Gets runtime contained-item snapshots keyed by child serial.
    /// This cache is not used for persistence.
    /// </summary>
    [MemoryPackIgnore]
    public IReadOnlyDictionary<Serial, ItemReference> ContainedItemReferences => _containedItemReferences;

    /// <summary>
    /// Gets typed custom properties stored for this item.
    /// </summary>
    [MemoryPackIgnore]
    public IReadOnlyDictionary<string, ItemCustomProperty> CustomProperties => _customProperties;

    [MemoryPackOnDeserialized]
    private void OnMemoryPackDeserialized()
    {
        Amount = Amount <= 0 ? 1 : Amount;
        _customProperties = _customProperties.Count == 0
            ? new(StringComparer.OrdinalIgnoreCase)
            : new(_customProperties, StringComparer.OrdinalIgnoreCase);
    }

    [MemoryPackIgnore]
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
    /// Clears all custom properties.
    /// </summary>
    public void ClearCustomProperties()
        => _customProperties.Clear();

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(Id);
        hash.Add(Location);
        hash.Add(MapId);
        hash.Add(Name, StringComparer.Ordinal);
        hash.Add(Weight);
        hash.Add(Amount);
        hash.Add(ItemId);
        hash.Add(Hue);
        hash.Add(GumpId);
        hash.Add(Direction);
        hash.Add(IsStackable);
        hash.Add(ScriptId, StringComparer.Ordinal);
        hash.Add(Rarity);
        hash.Add(Visibility);
        hash.Add(CombatStats?.MinStrength ?? 0);
        hash.Add(CombatStats?.MinDexterity ?? 0);
        hash.Add(CombatStats?.MinIntelligence ?? 0);
        hash.Add(CombatStats?.DamageMin ?? 0);
        hash.Add(CombatStats?.DamageMax ?? 0);
        hash.Add(CombatStats?.Defense ?? 0);
        hash.Add(CombatStats?.AttackSpeed ?? 0);
        hash.Add(CombatStats?.RangeMin ?? 0);
        hash.Add(CombatStats?.RangeMax ?? 0);
        hash.Add(CombatStats?.MaxDurability ?? 0);
        hash.Add(CombatStats?.CurrentDurability ?? 0);
        hash.Add(Modifiers?.StrengthBonus ?? 0);
        hash.Add(Modifiers?.DexterityBonus ?? 0);
        hash.Add(Modifiers?.IntelligenceBonus ?? 0);
        hash.Add(Modifiers?.PhysicalResist ?? 0);
        hash.Add(Modifiers?.FireResist ?? 0);
        hash.Add(Modifiers?.ColdResist ?? 0);
        hash.Add(Modifiers?.PoisonResist ?? 0);
        hash.Add(Modifiers?.EnergyResist ?? 0);
        hash.Add(Modifiers?.HitChanceIncrease ?? 0);
        hash.Add(Modifiers?.DefenseChanceIncrease ?? 0);
        hash.Add(Modifiers?.DamageIncrease ?? 0);
        hash.Add(Modifiers?.SwingSpeedIncrease ?? 0);
        hash.Add(Modifiers?.SpellDamageIncrease ?? 0);
        hash.Add(Modifiers?.FasterCasting ?? 0);
        hash.Add(Modifiers?.FasterCastRecovery ?? 0);
        hash.Add(Modifiers?.LowerManaCost ?? 0);
        hash.Add(Modifiers?.LowerReagentCost ?? 0);
        hash.Add(Modifiers?.Luck ?? 0);
        hash.Add(Modifiers?.SpellChanneling ?? 0);
        hash.Add(Modifiers?.UsesRemaining ?? 0);
        hash.Add(ParentContainerId);
        hash.Add(ContainerPosition);
        hash.Add(EquippedMobileId);
        hash.Add(EquippedLayer);

        foreach (var containedItemId in ContainedItemIds)
        {
            hash.Add(containedItemId);
        }

        foreach (var customProperty in _customProperties.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            hash.Add(customProperty.Key, StringComparer.Ordinal);
            hash.Add(customProperty.Value.Type);
            hash.Add(customProperty.Value.IntegerValue);
            hash.Add(customProperty.Value.BooleanValue);
            hash.Add(customProperty.Value.DoubleValue);
            hash.Add(customProperty.Value.StringValue, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
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
    /// Removes a custom property by key.
    /// </summary>
    /// <param name="key">Property key.</param>
    /// <returns><c>true</c> when removed; otherwise <c>false</c>.</returns>
    public bool RemoveCustomProperty(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _customProperties.Remove(key);
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

    /// <summary>
    /// Sets a boolean custom property.
    /// </summary>
    /// <param name="key">Property key.</param>
    /// <param name="value">Boolean value.</param>
    public void SetCustomBoolean(string key, bool value)
        => SetCustomProperty(
            key,
            new()
            {
                Type = ItemCustomPropertyType.Boolean,
                BooleanValue = value
            }
        );

    /// <summary>
    /// Sets a double custom property.
    /// </summary>
    /// <param name="key">Property key.</param>
    /// <param name="value">Double value.</param>
    public void SetCustomDouble(string key, double value)
        => SetCustomProperty(
            key,
            new()
            {
                Type = ItemCustomPropertyType.Double,
                DoubleValue = value
            }
        );

    /// <summary>
    /// Sets an integer custom property.
    /// </summary>
    /// <param name="key">Property key.</param>
    /// <param name="value">Integer value.</param>
    public void SetCustomInteger(string key, long value)
        => SetCustomProperty(
            key,
            new()
            {
                Type = ItemCustomPropertyType.Integer,
                IntegerValue = value
            }
        );

    /// <summary>
    /// Sets a location custom property serialized as a point string.
    /// </summary>
    /// <param name="key">Property key.</param>
    /// <param name="value">Location value.</param>
    public void SetCustomLocation(string key, Point3D value)
        => SetCustomString(key, value.ToString());

    /// <summary>
    /// Sets or replaces a typed custom property.
    /// </summary>
    /// <param name="key">Property key.</param>
    /// <param name="property">Property value.</param>
    public void SetCustomProperty(string key, ItemCustomProperty property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(property);
        _customProperties[key] = property;
    }

    /// <summary>
    /// Sets a string custom property.
    /// </summary>
    /// <param name="key">Property key.</param>
    /// <param name="value">String value.</param>
    public void SetCustomString(string key, string? value)
        => SetCustomProperty(
            key,
            new()
            {
                Type = ItemCustomPropertyType.String,
                StringValue = value
            }
        );

    public override string ToString()
        => $"Item(Id={Id}, Name={Name}, ItemId=0x{ItemId:X4}, MapId={MapId}, Location={Location})";

    /// <summary>
    /// Tries to get a boolean custom property.
    /// </summary>
    /// <param name="key">Property key.</param>
    /// <param name="value">Boolean value when found and typed correctly.</param>
    /// <returns><c>true</c> when found; otherwise <c>false</c>.</returns>
    public bool TryGetCustomBoolean(string key, out bool value)
    {
        value = false;

        if (!_customProperties.TryGetValue(key, out var property) || property.Type != ItemCustomPropertyType.Boolean)
        {
            return false;
        }

        value = property.BooleanValue;

        return true;
    }

    /// <summary>
    /// Tries to get a double custom property.
    /// </summary>
    /// <param name="key">Property key.</param>
    /// <param name="value">Double value when found and typed correctly.</param>
    /// <returns><c>true</c> when found; otherwise <c>false</c>.</returns>
    public bool TryGetCustomDouble(string key, out double value)
    {
        value = 0;

        if (!_customProperties.TryGetValue(key, out var property) || property.Type != ItemCustomPropertyType.Double)
        {
            return false;
        }

        value = property.DoubleValue;

        return true;
    }

    /// <summary>
    /// Tries to get an integer custom property.
    /// </summary>
    /// <param name="key">Property key.</param>
    /// <param name="value">Integer value when found and typed correctly.</param>
    /// <returns><c>true</c> when found; otherwise <c>false</c>.</returns>
    public bool TryGetCustomInteger(string key, out long value)
    {
        value = 0;

        if (!_customProperties.TryGetValue(key, out var property) || property.Type != ItemCustomPropertyType.Integer)
        {
            return false;
        }

        value = property.IntegerValue;

        return true;
    }

    /// <summary>
    /// Tries to get a location custom property encoded as a point string.
    /// </summary>
    /// <param name="key">Property key.</param>
    /// <param name="value">Location value when found and valid.</param>
    /// <returns><c>true</c> when found and parsed; otherwise <c>false</c>.</returns>
    public bool TryGetCustomLocation(string key, out Point3D value)
    {
        value = default;

        if (!TryGetCustomString(key, out var stringValue) || string.IsNullOrWhiteSpace(stringValue))
        {
            return false;
        }

        if (!Point3D.TryParse(stringValue, null, out var parsed))
        {
            return false;
        }

        value = parsed;

        return true;
    }

    /// <summary>
    /// Tries to get a string custom property.
    /// </summary>
    /// <param name="key">Property key.</param>
    /// <param name="value">String value when found and typed correctly.</param>
    /// <returns><c>true</c> when found; otherwise <c>false</c>.</returns>
    public bool TryGetCustomString(string key, out string? value)
    {
        value = null;

        if (!_customProperties.TryGetValue(key, out var property) || property.Type != ItemCustomPropertyType.String)
        {
            return false;
        }

        value = property.StringValue;

        return true;
    }

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
