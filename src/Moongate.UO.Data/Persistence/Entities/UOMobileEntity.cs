using Moongate.UO.Data.Bodies;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Entities;
using Moongate.UO.Data.Professions;
using Moongate.UO.Data.Races.Base;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;
using UoMap = Moongate.UO.Data.Maps.Map;

namespace Moongate.UO.Data.Persistence.Entities;

/// <summary>
/// Minimal mobile entity implementation used by race and map systems.
/// </summary>
public class UOMobileEntity : IMobileEntity
{
    private const int GoldItemId = 0x0EED;
    private const int DefaultSkillCap = 1000;
    private readonly Dictionary<ItemLayerType, ItemReference> _equippedItemReferences = [];
    private readonly Dictionary<ItemLayerType, UOItemEntity> _equippedItemsRuntime = [];
    private readonly Dictionary<string, ItemCustomProperty> _customProperties = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the unique mobile serial.
    /// </summary>
    public Serial Id { get; set; }

    /// <summary>
    /// Gets or sets the owning account serial when this mobile belongs to a player account.
    /// </summary>
    public Serial AccountId { get; set; }

    /// <summary>
    /// Gets or sets the mobile display name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the optional title shown with the mobile name.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the configured brain identifier used by scripted AI.
    /// </summary>
    public string? BrainId { get; set; }

    /// <summary>
    /// Gets or sets the mobile world location.
    /// </summary>
    public Point3D Location { get; set; }

    /// <summary>
    /// Gets or sets the world map identifier.
    /// </summary>
    public int MapId { get; set; }

    /// <summary>
    /// Gets or sets the resolved world map for this mobile.
    /// </summary>
    public UoMap? Map
    {
        get => UoMap.GetMap(MapId);
        set => MapId = value?.Index ?? 0;
    }

    /// <summary>
    /// Gets or sets the world-facing direction.
    /// </summary>
    public DirectionType Direction { get; set; }

    /// <summary>
    /// Gets or sets whether this mobile is player-controlled.
    /// </summary>
    public bool IsPlayer { get; set; }

    /// <summary>
    /// Gets or sets whether this mobile is alive.
    /// </summary>
    public bool IsAlive { get; set; } = true;

    /// <summary>
    /// Gets or sets the mobile gender.
    /// </summary>
    public GenderType Gender { get; set; }

    /// <summary>
    /// Gets or sets the race table index.
    /// </summary>
    public byte RaceIndex { get; set; }

    /// <summary>
    /// Gets or sets the resolved race descriptor.
    /// </summary>
    public Race? Race
    {
        get => RaceIndex < Race.Races.Length ? Race.Races[RaceIndex] : null;
        set => RaceIndex = value is null ? (byte)0 : (byte)value.RaceIndex;
    }

    /// <summary>
    /// Gets or sets the profession identifier.
    /// </summary>
    public int ProfessionId { get; set; }

    /// <summary>
    /// Gets or sets the resolved profession descriptor.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the skin hue.
    /// </summary>
    public short SkinHue { get; set; }

    /// <summary>
    /// Gets or sets the hair style.
    /// </summary>
    public short HairStyle { get; set; }

    /// <summary>
    /// Gets or sets the hair hue.
    /// </summary>
    public short HairHue { get; set; }

    /// <summary>
    /// Gets or sets the facial hair style.
    /// </summary>
    public short FacialHairStyle { get; set; }

    /// <summary>
    /// Gets or sets the facial hair hue.
    /// </summary>
    public short FacialHairHue { get; set; }

    /// <summary>
    /// Gets or sets the explicit base body override.
    /// </summary>
    public Body? BaseBody { get; set; }

    /// <summary>
    /// Gets or sets the resolved body value exposed to packets and gameplay systems.
    /// </summary>
    public Body Body
    {
        get => GetBody();
        set => SetBody(value);
    }

    /// <summary>
    /// Gets or sets the persisted base stat values.
    /// </summary>
    public MobileStats BaseStats { get; set; } = new();

    /// <summary>
    /// Gets or sets the persisted base resistance values.
    /// </summary>
    public MobileResistances BaseResistances { get; set; } = new();

    /// <summary>
    /// Gets or sets the current and maximum resource values.
    /// </summary>
    public MobileResources Resources { get; set; } = new();

    /// <summary>
    /// Gets or sets the aggregated modifiers coming from equipped items.
    /// </summary>
    public MobileModifiers? EquipmentModifiers { get; set; }

    /// <summary>
    /// Gets or sets the aggregated runtime modifiers coming from buffs and debuffs.
    /// </summary>
    public MobileModifiers? RuntimeModifiers { get; set; }

    /// <summary>
    /// Gets or sets the modifier cap values used by modern status packets and effect validation.
    /// </summary>
    public MobileModifierCaps ModifierCaps { get; set; } = new();

    /// <summary>
    /// Gets or sets the base strength value.
    /// </summary>
    public int Strength
    {
        get => BaseStats.Strength;
        set => BaseStats.Strength = value;
    }

    /// <summary>
    /// Gets or sets the base dexterity value.
    /// </summary>
    public int Dexterity
    {
        get => BaseStats.Dexterity;
        set => BaseStats.Dexterity = value;
    }

    /// <summary>
    /// Gets or sets the base intelligence value.
    /// </summary>
    public int Intelligence
    {
        get => BaseStats.Intelligence;
        set => BaseStats.Intelligence = value;
    }

    /// <summary>
    /// Gets or sets the current hit points.
    /// </summary>
    public int Hits
    {
        get => Resources.Hits;
        set => Resources.Hits = value;
    }

    /// <summary>
    /// Gets or sets the current mana value.
    /// </summary>
    public int Mana
    {
        get => Resources.Mana;
        set => Resources.Mana = value;
    }

    /// <summary>
    /// Gets or sets the current stamina value.
    /// </summary>
    public int Stamina
    {
        get => Resources.Stamina;
        set => Resources.Stamina = value;
    }

    /// <summary>
    /// Gets or sets the maximum hit points.
    /// </summary>
    public int MaxHits
    {
        get => Resources.MaxHits;
        set => Resources.MaxHits = value;
    }

    /// <summary>
    /// Gets or sets the maximum mana value.
    /// </summary>
    public int MaxMana
    {
        get => Resources.MaxMana;
        set => Resources.MaxMana = value;
    }

    /// <summary>
    /// Gets or sets the maximum stamina value.
    /// </summary>
    public int MaxStamina
    {
        get => Resources.MaxStamina;
        set => Resources.MaxStamina = value;
    }

    /// <summary>
    /// Gets or sets the remaining unallocated skill points, when used by creation or progression flows.
    /// </summary>
    public int SkillPoints { get; set; }

    /// <summary>
    /// Gets or sets the remaining unallocated stat points, when used by creation or progression flows.
    /// </summary>
    public int StatPoints { get; set; }

    /// <summary>
    /// Gets or sets the total stat cap.
    /// </summary>
    public int StatCap { get; set; } = 225;

    /// <summary>
    /// Gets or sets the current follower slot usage.
    /// </summary>
    public int Followers { get; set; }

    /// <summary>
    /// Gets or sets the maximum follower slots.
    /// </summary>
    public int FollowersMax { get; set; } = 5;

    /// <summary>
    /// Gets or sets the carried weight used by the modern status packet.
    /// </summary>
    public int Weight { get; set; }

    /// <summary>
    /// Gets or sets the carrying capacity used by the modern status packet.
    /// </summary>
    public int MaxWeight { get; set; }

    /// <summary>
    /// Gets or sets the minimum weapon damage shown in the status packet.
    /// </summary>
    public int MinWeaponDamage { get; set; }

    /// <summary>
    /// Gets or sets the maximum weapon damage shown in the status packet.
    /// </summary>
    public int MaxWeaponDamage { get; set; }

    /// <summary>
    /// Gets or sets the tithing points shown in the status packet.
    /// </summary>
    public int Tithing { get; set; }

    /// <summary>
    /// Gets or sets the persisted skill table keyed by UO skill id.
    /// </summary>
    public Dictionary<UOSkillName, SkillEntry> Skills { get; set; } = [];

    /// <summary>
    /// Gets or sets the base fire resistance.
    /// </summary>
    public int FireResistance
    {
        get => BaseResistances.Fire;
        set => BaseResistances.Fire = value;
    }

    /// <summary>
    /// Gets or sets the base cold resistance.
    /// </summary>
    public int ColdResistance
    {
        get => BaseResistances.Cold;
        set => BaseResistances.Cold = value;
    }

    /// <summary>
    /// Gets or sets the base poison resistance.
    /// </summary>
    public int PoisonResistance
    {
        get => BaseResistances.Poison;
        set => BaseResistances.Poison = value;
    }

    /// <summary>
    /// Gets or sets the base energy resistance.
    /// </summary>
    public int EnergyResistance
    {
        get => BaseResistances.Energy;
        set => BaseResistances.Energy = value;
    }

    /// <summary>
    /// Gets or sets the base luck value before modifiers are applied.
    /// </summary>
    public int BaseLuck { get; set; }

    /// <summary>
    /// Gets or sets the legacy luck alias backed by <see cref="BaseLuck" />.
    /// </summary>
    public int Luck
    {
        get => BaseLuck;
        set => BaseLuck = value;
    }

    /// <summary>
    /// Gets the effective strength after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveStrength => Strength + GetModifierValue(static modifier => modifier.StrengthBonus);

    /// <summary>
    /// Gets the effective dexterity after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveDexterity => Dexterity + GetModifierValue(static modifier => modifier.DexterityBonus);

    /// <summary>
    /// Gets the effective intelligence after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveIntelligence => Intelligence + GetModifierValue(static modifier => modifier.IntelligenceBonus);

    /// <summary>
    /// Gets the effective physical resistance after equipment and runtime modifiers.
    /// </summary>
    public int EffectivePhysicalResistance
        => BaseResistances.Physical + GetModifierValue(static modifier => modifier.PhysicalResist);

    /// <summary>
    /// Gets the effective fire resistance after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveFireResistance => FireResistance + GetModifierValue(static modifier => modifier.FireResist);

    /// <summary>
    /// Gets the effective cold resistance after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveColdResistance => ColdResistance + GetModifierValue(static modifier => modifier.ColdResist);

    /// <summary>
    /// Gets the effective poison resistance after equipment and runtime modifiers.
    /// </summary>
    public int EffectivePoisonResistance => PoisonResistance + GetModifierValue(static modifier => modifier.PoisonResist);

    /// <summary>
    /// Gets the effective energy resistance after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveEnergyResistance => EnergyResistance + GetModifierValue(static modifier => modifier.EnergyResist);

    /// <summary>
    /// Gets the effective luck after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveLuck => BaseLuck + GetModifierValue(static modifier => modifier.Luck);

    /// <summary>
    /// Gets the effective hit chance increase after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveHitChanceIncrease => GetModifierValue(static modifier => modifier.HitChanceIncrease);

    /// <summary>
    /// Gets the effective defense chance increase after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveDefenseChanceIncrease => GetModifierValue(static modifier => modifier.DefenseChanceIncrease);

    /// <summary>
    /// Gets the effective damage increase after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveDamageIncrease => GetModifierValue(static modifier => modifier.DamageIncrease);

    /// <summary>
    /// Gets the effective swing speed increase after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveSwingSpeedIncrease => GetModifierValue(static modifier => modifier.SwingSpeedIncrease);

    /// <summary>
    /// Gets the effective spell damage increase after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveSpellDamageIncrease => GetModifierValue(static modifier => modifier.SpellDamageIncrease);

    /// <summary>
    /// Gets the effective faster casting value after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveFasterCasting => GetModifierValue(static modifier => modifier.FasterCasting);

    /// <summary>
    /// Gets the effective faster cast recovery value after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveFasterCastRecovery => GetModifierValue(static modifier => modifier.FasterCastRecovery);

    /// <summary>
    /// Gets the effective lower mana cost after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveLowerManaCost => GetModifierValue(static modifier => modifier.LowerManaCost);

    /// <summary>
    /// Gets the effective lower reagent cost after equipment and runtime modifiers.
    /// </summary>
    public int EffectiveLowerReagentCost => GetModifierValue(static modifier => modifier.LowerReagentCost);

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

    /// <summary>
    /// Gets runtime total gold in backpack and bank box.
    /// </summary>
    public int Gold => GetGold();

    /// <summary>
    /// Gets persisted custom mobile properties.
    /// </summary>
    public IReadOnlyDictionary<string, ItemCustomProperty> CustomProperties => _customProperties;

    /// <summary>
    /// Gets or sets whether the mobile is in war mode.
    /// </summary>
    public bool IsWarMode { get; set; }

    /// <summary>
    /// Gets or sets the hunger level.
    /// </summary>
    public int Hunger { get; set; }

    /// <summary>
    /// Gets or sets the thirst level.
    /// </summary>
    public int Thirst { get; set; }

    /// <summary>
    /// Gets or sets the fame value.
    /// </summary>
    public int Fame { get; set; }

    /// <summary>
    /// Gets or sets the karma value.
    /// </summary>
    public int Karma { get; set; }

    /// <summary>
    /// Gets or sets the murder count.
    /// </summary>
    public int Kills { get; set; }

    /// <summary>
    /// Gets or sets the legacy hidden alias backed by <see cref="IsHidden" />.
    /// </summary>
    public bool Hidden
    {
        get => IsHidden;
        set => IsHidden = value;
    }

    /// <summary>
    /// Gets or sets whether the mobile is hidden.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Gets or sets whether the mobile is frozen.
    /// </summary>
    public bool IsFrozen { get; set; }

    /// <summary>
    /// Gets or sets whether the mobile is paralyzed.
    /// </summary>
    public bool IsParalyzed { get; set; }

    /// <summary>
    /// Gets or sets whether the mobile is flying.
    /// </summary>
    public bool IsFlying { get; set; }

    /// <summary>
    /// Gets or sets whether the mobile ignores collision with other mobiles.
    /// </summary>
    public bool IgnoreMobiles { get; set; }

    /// <summary>
    /// Gets or sets whether the mobile is poisoned.
    /// </summary>
    public bool IsPoisoned { get; set; }

    /// <summary>
    /// Gets or sets the legacy blessed alias backed by <see cref="IsBlessed" />.
    /// </summary>
    public bool Blessed
    {
        get => IsBlessed;
        set => IsBlessed = value;
    }

    /// <summary>
    /// Gets or sets whether the mobile is blessed.
    /// </summary>
    public bool IsBlessed { get; set; }

    /// <summary>
    /// Gets or sets whether the mobile is invulnerable.
    /// </summary>
    public bool IsInvulnerable { get; set; }

    /// <summary>
    /// Gets or sets whether the mobile is mounted.
    /// </summary>
    public bool IsMounted { get; set; }

    /// <summary>
    /// Gets or sets the notoriety level.
    /// </summary>
    public Notoriety Notoriety { get; set; } = Notoriety.Innocent;

    /// <summary>
    /// Gets or sets the creation timestamp in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the last login timestamp in UTC.
    /// </summary>
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
    /// Applies a runtime modifier delta to the aggregated runtime modifiers.
    /// </summary>
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

    /// <summary>
    /// Resolves the current body value, falling back to race defaults when needed.
    /// </summary>
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

    /// <summary>
    /// Gets the runtime equipped item entities resolved for this mobile.
    /// </summary>
    public IReadOnlyCollection<UOItemEntity> GetEquippedItemsRuntime()
        => _equippedItemsRuntime.Values;

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
    /// Gets a skill entry by skill name when present.
    /// </summary>
    public SkillEntry? GetSkill(UOSkillName skillName)
        => Skills.GetValueOrDefault(skillName);

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

    /// <summary>
    /// Initializes the persisted skill table from the loaded skill metadata table.
    /// </summary>
    public void InitializeSkills()
    {
        Skills.Clear();

        foreach (var skillInfo in SkillInfo.Table)
        {
            if (!Enum.IsDefined(typeof(UOSkillName), skillInfo.SkillID))
            {
                continue;
            }

            Skills[(UOSkillName)skillInfo.SkillID] = CreateSkillEntry(skillInfo);
        }
    }

    /// <summary>
    /// Overrides the resolved body with an explicit body value.
    /// </summary>
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

    /// <summary>
    /// Removes a runtime modifier delta from the aggregated runtime modifiers.
    /// </summary>
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

    /// <summary>
    /// Sets the explicit base body override.
    /// </summary>
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

    /// <summary>
    /// Sets or replaces a persisted skill entry.
    /// </summary>
    public SkillEntry SetSkill(
        UOSkillName skillName,
        int value,
        int? baseValue = null,
        int cap = DefaultSkillCap,
        UOSkillLock lockState = UOSkillLock.Up
    )
    {
        var skillInfo = ResolveSkillInfo(skillName);
        var entry = Skills.TryGetValue(skillName, out var existing) ? existing : CreateSkillEntry(skillInfo);

        entry.Skill = skillInfo;
        entry.Value = value;
        entry.Base = baseValue ?? value;
        entry.Cap = cap;
        entry.Lock = lockState;
        Skills[skillName] = entry;

        return entry;
    }

    /// <summary>
    /// Returns a diagnostic string for the mobile.
    /// </summary>
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

    private static SkillEntry CreateSkillEntry(SkillInfo skillInfo)
        => new()
        {
            Skill = skillInfo,
            Value = 0,
            Base = 0,
            Cap = DefaultSkillCap,
            Lock = UOSkillLock.Up
        };

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

    private static SkillInfo ResolveSkillInfo(UOSkillName skillName)
    {
        foreach (var skillInfo in SkillInfo.Table)
        {
            if (skillInfo.SkillID == (int)skillName)
            {
                return skillInfo;
            }
        }

        return new(
            (int)skillName,
            skillName.ToString(),
            0,
            0,
            0,
            string.Empty,
            0,
            0,
            0,
            1,
            skillName.ToString(),
            Stat.Strength,
            Stat.Strength
        );
    }

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
}
