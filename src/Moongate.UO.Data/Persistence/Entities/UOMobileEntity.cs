using Moongate.UO.Data.Bodies;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Entities;
using Moongate.UO.Data.Professions;
using Moongate.UO.Data.Races.Base;
using Moongate.UO.Data.Types;
using UoMap = Moongate.UO.Data.Maps.Map;

namespace Moongate.UO.Data.Persistence.Entities;

/// <summary>
/// Minimal mobile entity implementation used by race and map systems.
/// </summary>
public class UOMobileEntity : IMobileEntity
{
    private const int GoldItemId = 0x0EED;
    private readonly Dictionary<ItemLayerType, ItemReference> _equippedItemReferences = [];
    private readonly Dictionary<ItemLayerType, UOItemEntity> _equippedItemsRuntime = [];
    private readonly Dictionary<string, ItemCustomProperty> _customProperties = new(StringComparer.Ordinal);

    public Serial Id { get; set; }

    public Serial AccountId { get; set; }

    public string? Name { get; set; }

    public string? Title { get; set; }

    public string? BrainId { get; set; }

    public Point3D Location { get; set; }

    public int MapId { get; set; }

    public UoMap? Map
    {
        get => UoMap.GetMap(MapId);
        set => MapId = value?.Index ?? 0;
    }

    public DirectionType Direction { get; set; }

    public bool IsPlayer { get; set; }

    public bool IsAlive { get; set; } = true;

    public GenderType Gender { get; set; }

    public byte RaceIndex { get; set; }

    public Race? Race
    {
        get => RaceIndex < Race.Races.Length ? Race.Races[RaceIndex] : null;
        set => RaceIndex = value is null ? (byte)0 : (byte)value.RaceIndex;
    }

    public int ProfessionId { get; set; }

    public ProfessionInfo Profession
    {
        get
        {
            if (ProfessionInfo.Professions is { Length: > 0 } &&
                ProfessionInfo.GetProfession(ProfessionId, out var profession))
            {
                return profession;
            }

            return new() { ID = ProfessionId };
        }
        set => ProfessionId = value?.ID ?? 0;
    }

    public short SkinHue { get; set; }

    public short HairStyle { get; set; }

    public short HairHue { get; set; }

    public short FacialHairStyle { get; set; }

    public short FacialHairHue { get; set; }

    public Body? BaseBody { get; set; }

    public Body Body
    {
        get => GetBody();
        set => SetBody(value);
    }

    public MobileStats BaseStats { get; set; } = new();

    public MobileResistances BaseResistances { get; set; } = new();

    public MobileResources Resources { get; set; } = new();

    public MobileModifiers? EquipmentModifiers { get; set; }

    public MobileModifiers? RuntimeModifiers { get; set; }

    public int Strength
    {
        get => BaseStats.Strength;
        set => BaseStats.Strength = value;
    }

    public int Dexterity
    {
        get => BaseStats.Dexterity;
        set => BaseStats.Dexterity = value;
    }

    public int Intelligence
    {
        get => BaseStats.Intelligence;
        set => BaseStats.Intelligence = value;
    }

    public int Hits
    {
        get => Resources.Hits;
        set => Resources.Hits = value;
    }

    public int Mana
    {
        get => Resources.Mana;
        set => Resources.Mana = value;
    }

    public int Stamina
    {
        get => Resources.Stamina;
        set => Resources.Stamina = value;
    }

    public int MaxHits
    {
        get => Resources.MaxHits;
        set => Resources.MaxHits = value;
    }

    public int MaxMana
    {
        get => Resources.MaxMana;
        set => Resources.MaxMana = value;
    }

    public int MaxStamina
    {
        get => Resources.MaxStamina;
        set => Resources.MaxStamina = value;
    }

    public int SkillPoints { get; set; }

    public int StatPoints { get; set; }

    public int FireResistance
    {
        get => BaseResistances.Fire;
        set => BaseResistances.Fire = value;
    }

    public int ColdResistance
    {
        get => BaseResistances.Cold;
        set => BaseResistances.Cold = value;
    }

    public int PoisonResistance
    {
        get => BaseResistances.Poison;
        set => BaseResistances.Poison = value;
    }

    public int EnergyResistance
    {
        get => BaseResistances.Energy;
        set => BaseResistances.Energy = value;
    }

    public int BaseLuck { get; set; }

    public int Luck
    {
        get => BaseLuck;
        set => BaseLuck = value;
    }

    public int EffectiveStrength => Strength + GetModifierValue(static modifier => modifier.StrengthBonus);

    public int EffectiveDexterity => Dexterity + GetModifierValue(static modifier => modifier.DexterityBonus);

    public int EffectiveIntelligence => Intelligence + GetModifierValue(static modifier => modifier.IntelligenceBonus);

    public int EffectivePhysicalResistance
        => BaseResistances.Physical + GetModifierValue(static modifier => modifier.PhysicalResist);

    public int EffectiveFireResistance
        => FireResistance + GetModifierValue(static modifier => modifier.FireResist);

    public int EffectiveColdResistance
        => ColdResistance + GetModifierValue(static modifier => modifier.ColdResist);

    public int EffectivePoisonResistance
        => PoisonResistance + GetModifierValue(static modifier => modifier.PoisonResist);

    public int EffectiveEnergyResistance
        => EnergyResistance + GetModifierValue(static modifier => modifier.EnergyResist);

    public int EffectiveLuck => BaseLuck + GetModifierValue(static modifier => modifier.Luck);

    /// <summary>
    /// Gets or sets the serial of the backpack item.
    /// </summary>
    public Serial BackpackId { get; set; }

    /// <summary>
    /// Gets equipped item references by layer.
    /// </summary>
    public Dictionary<ItemLayerType, Serial> EquippedItemIds { get; set; } = [];

    /// <summary>
    /// Gets runtime equipped-item snapshots keyed by equipment layer.
    /// This cache is not used for persistence.
    /// </summary>
    public IReadOnlyDictionary<ItemLayerType, ItemReference> EquippedItemReferences => _equippedItemReferences;

    public IReadOnlyCollection<UOItemEntity> GetEquippedItemsRuntime()
        => _equippedItemsRuntime.Values;

    /// <summary>
    /// Gets runtime total gold in backpack and bank box.
    /// </summary>
    public int Gold => GetGold();

    /// <summary>
    /// Gets persisted custom mobile properties.
    /// </summary>
    public IReadOnlyDictionary<string, ItemCustomProperty> CustomProperties => _customProperties;

    public bool IsWarMode { get; set; }

    public int Hunger { get; set; }

    public int Thirst { get; set; }

    public int Fame { get; set; }

    public int Karma { get; set; }

    public int Kills { get; set; }

    public bool Hidden
    {
        get => IsHidden;
        set => IsHidden = value;
    }

    public bool IsHidden { get; set; }

    public bool IsFrozen { get; set; }

    public bool IsParalyzed { get; set; }

    public bool IsFlying { get; set; }

    public bool IgnoreMobiles { get; set; }

    public bool IsPoisoned { get; set; }

    public bool Blessed
    {
        get => IsBlessed;
        set => IsBlessed = value;
    }

    public bool IsBlessed { get; set; }

    public bool IsInvulnerable { get; set; }

    public bool IsMounted { get; set; }

    public Notoriety Notoriety { get; set; } = Notoriety.Innocent;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastLoginUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Associates an equipped item with this mobile and updates item ownership metadata.
    /// </summary>
    public void AddEquippedItem(ItemLayerType layer, UOItemEntity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        EquippedItemIds[layer] = item.Id;
        _equippedItemReferences[layer] = new(item.Id, item.ItemId, item.Hue);
        _equippedItemsRuntime[layer] = item;
        item.ParentContainerId = Serial.Zero;
        item.ContainerPosition = Point2D.Zero;
        item.EquippedMobileId = Id;
        item.EquippedLayer = layer;
    }

    /// <summary>
    /// Associates an equipped item id with this mobile without item metadata updates.
    /// </summary>
    public void AddEquippedItem(ItemLayerType layer, Serial itemId)
    {
        EquippedItemIds[layer] = itemId;
        _equippedItemReferences.Remove(layer);
        _equippedItemsRuntime.Remove(layer);
    }

    /// <summary>
    /// Clears all custom properties.
    /// </summary>
    public void ClearCustomProperties()
        => _customProperties.Clear();

    /// <summary>
    /// Equips an item and updates both persisted references and runtime cache.
    /// </summary>
    /// <param name="layer">Target item layer.</param>
    /// <param name="item">Equipped item entity.</param>
    public void EquipItem(ItemLayerType layer, UOItemEntity item)
        => AddEquippedItem(layer, item);

    public virtual Body GetBody()
    {
        if (BaseBody is Body baseBody)
        {
            if (baseBody == 0x00)
            {
                var raceForAliveBody = Race;

                return raceForAliveBody is null ? 0x00 : (Body)raceForAliveBody.Body(this);
            }

            return baseBody;
        }

        var fallbackRace = Race ?? (Race.Races.Length > 0 ? Race.Races[0] : null);

        return fallbackRace is null ? 0x00 : (Body)fallbackRace.Body(this);
    }

    public void ApplyRuntimeModifier(MobileModifierDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        RuntimeModifiers ??= new();

        RuntimeModifiers.StrengthBonus += delta.StrengthBonus;
        RuntimeModifiers.DexterityBonus += delta.DexterityBonus;
        RuntimeModifiers.IntelligenceBonus += delta.IntelligenceBonus;
        RuntimeModifiers.PhysicalResist += delta.PhysicalResist;
        RuntimeModifiers.FireResist += delta.FireResist;
        RuntimeModifiers.ColdResist += delta.ColdResist;
        RuntimeModifiers.PoisonResist += delta.PoisonResist;
        RuntimeModifiers.EnergyResist += delta.EnergyResist;
        RuntimeModifiers.HitChanceIncrease += delta.HitChanceIncrease;
        RuntimeModifiers.DefenseChanceIncrease += delta.DefenseChanceIncrease;
        RuntimeModifiers.DamageIncrease += delta.DamageIncrease;
        RuntimeModifiers.SwingSpeedIncrease += delta.SwingSpeedIncrease;
        RuntimeModifiers.SpellDamageIncrease += delta.SpellDamageIncrease;
        RuntimeModifiers.FasterCasting += delta.FasterCasting;
        RuntimeModifiers.FasterCastRecovery += delta.FasterCastRecovery;
        RuntimeModifiers.LowerManaCost += delta.LowerManaCost;
        RuntimeModifiers.LowerReagentCost += delta.LowerReagentCost;
        RuntimeModifiers.Luck += delta.Luck;
        RuntimeModifiers.SpellChanneling += delta.SpellChanneling;
    }

    /// <summary>
    /// Gets runtime equipped-item reference for a layer, if present.
    /// </summary>
    /// <param name="layer">Equipment layer.</param>
    /// <returns>Runtime equipped item reference; otherwise <c>null</c>.</returns>
    public ItemReference? GetEquippedReference(ItemLayerType layer)
    {
        if (_equippedItemReferences.TryGetValue(layer, out var itemReference))
        {
            return itemReference;
        }

        return null;
    }

    /// <summary>
    /// Calculates protocol packet flags for this mobile.
    /// </summary>
    /// <param name="stygianAbyss">
    /// Whether to use Stygian Abyss semantics (bit 0x04 is flying instead of poisoned).
    /// </param>
    /// <returns>Packet flags byte for mobile update packets.</returns>
    public virtual byte GetPacketFlags(bool stygianAbyss)
    {
        byte flags = 0x00;

        if (IsParalyzed || IsFrozen)
        {
            flags |= 0x01;
        }

        if (Gender == GenderType.Female)
        {
            flags |= 0x02;
        }

        if (stygianAbyss)
        {
            if (IsFlying)
            {
                flags |= 0x04;
            }
        }
        else
        {
            if (IsPoisoned)
            {
                flags |= 0x04;
            }
        }

        if (IsBlessed)
        {
            flags |= 0x08;
        }

        if (IgnoreMobiles)
        {
            flags |= 0x10;
            flags |= 0x40;
        }

        if (IsHidden)
        {
            flags |= 0x80;
        }

        return flags;
    }

    /// <summary>
    /// Gets whether an item is equipped in the specified layer.
    /// </summary>
    /// <param name="layer">Equipment layer.</param>
    /// <returns><c>true</c> when equipped.</returns>
    public bool HasEquippedItem(ItemLayerType layer)
        => EquippedItemIds.ContainsKey(layer);

    /// <summary>
    /// Hydrates runtime equipped-item references from resolved item entities.
    /// </summary>
    /// <param name="equippedItems">Resolved equipped items for this mobile.</param>
    public void HydrateEquipmentRuntime(IEnumerable<UOItemEntity> equippedItems)
    {
        ArgumentNullException.ThrowIfNull(equippedItems);

        _equippedItemReferences.Clear();
        _equippedItemsRuntime.Clear();

        foreach (var item in equippedItems)
        {
            if (item.EquippedMobileId != Id)
            {
                continue;
            }

            var layer = item.EquippedLayer;

            if (layer is null)
            {
                continue;
            }

            _equippedItemReferences[layer.Value] = new(item.Id, item.ItemId, item.Hue);
            _equippedItemsRuntime[layer.Value] = item;
        }
    }

    public void OverrideBody(Body body)
        => SetBody(body);

    /// <summary>
    /// Recomputes max stat caps from base stats and clamps current values.
    /// </summary>
    public void RecalculateMaxStats()
    {
        MaxHits = Math.Max(1, Strength);
        MaxMana = Math.Max(1, Intelligence);
        MaxStamina = Math.Max(1, Dexterity);

        Hits = Math.Min(Hits, MaxHits);
        Mana = Math.Min(Mana, MaxMana);
        Stamina = Math.Min(Stamina, MaxStamina);
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

    public void RemoveRuntimeModifier(MobileModifierDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        if (RuntimeModifiers is null)
        {
            return;
        }

        RuntimeModifiers.StrengthBonus -= delta.StrengthBonus;
        RuntimeModifiers.DexterityBonus -= delta.DexterityBonus;
        RuntimeModifiers.IntelligenceBonus -= delta.IntelligenceBonus;
        RuntimeModifiers.PhysicalResist -= delta.PhysicalResist;
        RuntimeModifiers.FireResist -= delta.FireResist;
        RuntimeModifiers.ColdResist -= delta.ColdResist;
        RuntimeModifiers.PoisonResist -= delta.PoisonResist;
        RuntimeModifiers.EnergyResist -= delta.EnergyResist;
        RuntimeModifiers.HitChanceIncrease -= delta.HitChanceIncrease;
        RuntimeModifiers.DefenseChanceIncrease -= delta.DefenseChanceIncrease;
        RuntimeModifiers.DamageIncrease -= delta.DamageIncrease;
        RuntimeModifiers.SwingSpeedIncrease -= delta.SwingSpeedIncrease;
        RuntimeModifiers.SpellDamageIncrease -= delta.SpellDamageIncrease;
        RuntimeModifiers.FasterCasting -= delta.FasterCasting;
        RuntimeModifiers.FasterCastRecovery -= delta.FasterCastRecovery;
        RuntimeModifiers.LowerManaCost -= delta.LowerManaCost;
        RuntimeModifiers.LowerReagentCost -= delta.LowerReagentCost;
        RuntimeModifiers.Luck -= delta.Luck;
        RuntimeModifiers.SpellChanneling -= delta.SpellChanneling;

        if (IsZero(RuntimeModifiers))
        {
            RuntimeModifiers = null;
        }
    }

    public void SetBody(Body body)
        => BaseBody = body;

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
        => $"Mobile(Id={Id}, IsPlayer={IsPlayer}, Location={Location})";

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
    /// Tries to get runtime equipped-item reference for a layer.
    /// </summary>
    /// <param name="layer">Equipment layer.</param>
    /// <param name="itemReference">Resolved runtime reference when found.</param>
    /// <returns><c>true</c> when runtime reference is available.</returns>
    public bool TryGetEquippedReference(ItemLayerType layer, out ItemReference itemReference)
        => _equippedItemReferences.TryGetValue(layer, out itemReference);

    /// <summary>
    /// Unequips an item layer and optionally clears metadata on a provided item instance.
    /// </summary>
    /// <param name="layer">Layer to unequip.</param>
    /// <param name="item">Optional equipped item instance to clear metadata on.</param>
    /// <returns><c>true</c> when a layer entry existed and was removed.</returns>
    public bool UnequipItem(ItemLayerType layer, UOItemEntity? item = null)
    {
        var removed = EquippedItemIds.Remove(layer);
        _equippedItemReferences.Remove(layer);
        _equippedItemsRuntime.Remove(layer);

        if (removed && item is not null)
        {
            item.EquippedMobileId = Serial.Zero;
            item.EquippedLayer = null;
        }

        return removed;
    }

    private int GetGold()
    {
        var visited = new HashSet<Serial>();
        long total = 0;

        if (TryGetBackpackRuntime(out var backpack))
        {
            total += SumGoldRecursive(backpack, visited);
        }

        if (TryGetBankBoxRuntime(out var bankBox))
        {
            total += SumGoldRecursive(bankBox, visited);
        }

        return total >= int.MaxValue ? int.MaxValue : (int)total;
    }

    private bool TryGetBackpackRuntime(out UOItemEntity backpack)
    {
        backpack = null!;

        if (_equippedItemsRuntime.TryGetValue(ItemLayerType.Backpack, out backpack))
        {
            return true;
        }

        if (BackpackId == Serial.Zero)
        {
            return false;
        }

        foreach (var equipped in _equippedItemsRuntime.Values)
        {
            if (equipped.Id != BackpackId)
            {
                continue;
            }

            backpack = equipped;

            return true;
        }

        return false;
    }

    private bool TryGetBankBoxRuntime(out UOItemEntity bankBox)
        => _equippedItemsRuntime.TryGetValue(ItemLayerType.Bank, out bankBox);

    private int GetModifierValue(Func<MobileModifiers, int> selector)
    {
        var total = 0;

        if (EquipmentModifiers is not null)
        {
            total += selector(EquipmentModifiers);
        }

        if (RuntimeModifiers is not null)
        {
            total += selector(RuntimeModifiers);
        }

        return total;
    }

    private static bool IsZero(MobileModifiers modifiers)
        => modifiers.StrengthBonus == 0 &&
           modifiers.DexterityBonus == 0 &&
           modifiers.IntelligenceBonus == 0 &&
           modifiers.PhysicalResist == 0 &&
           modifiers.FireResist == 0 &&
           modifiers.ColdResist == 0 &&
           modifiers.PoisonResist == 0 &&
           modifiers.EnergyResist == 0 &&
           modifiers.HitChanceIncrease == 0 &&
           modifiers.DefenseChanceIncrease == 0 &&
           modifiers.DamageIncrease == 0 &&
           modifiers.SwingSpeedIncrease == 0 &&
           modifiers.SpellDamageIncrease == 0 &&
           modifiers.FasterCasting == 0 &&
           modifiers.FasterCastRecovery == 0 &&
           modifiers.LowerManaCost == 0 &&
           modifiers.LowerReagentCost == 0 &&
           modifiers.Luck == 0 &&
           modifiers.SpellChanneling == 0;

    private static long SumGoldRecursive(UOItemEntity container, HashSet<Serial> visited)
    {
        if (!visited.Add(container.Id))
        {
            return 0;
        }

        long total = 0;

        foreach (var child in container.Items)
        {
            if (child.ItemId == GoldItemId)
            {
                total += Math.Max(0, child.Amount);
            }

            if (child.Items.Count > 0)
            {
                total += SumGoldRecursive(child, visited);
            }
        }

        return total;
    }
}
