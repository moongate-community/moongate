using MemoryPack;
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
[MemoryPackable(SerializeLayout.Explicit)]
public partial class UOMobileEntity : IMobileEntity
{
    private const int DefaultBodyWeight = 11;
    private const int HumanMaxWeightBase = 100;
    private const int NonHumanMaxWeightBase = 40;
    private const int GoldItemId = 0x0EED;
    private const int DefaultSkillCap = 1000;
    private const int DefaultUnarmedMinWeaponDamage = 1;
    private const int DefaultUnarmedMaxWeaponDamage = 4;
    private const uint MountVirtualSerialMask = 0x3EEEEEEE;
    private readonly Dictionary<ItemLayerType, ItemReference> _equippedItemReferences = [];
    private readonly Dictionary<ItemLayerType, UOItemEntity> _equippedItemsRuntime = [];

    [MemoryPackInclude]
    [MemoryPackOrder(38)]
    private Dictionary<string, ItemCustomProperty> _customProperties = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the unique mobile serial.
    /// </summary>
    [MemoryPackOrder(0)]
    public Serial Id { get; set; }

    /// <summary>
    /// Gets or sets the owning account serial when this mobile belongs to a player account.
    /// </summary>
    [MemoryPackOrder(1)]
    public Serial AccountId { get; set; }

    /// <summary>
    /// Gets or sets the mobile display name.
    /// </summary>
    [MemoryPackOrder(2)]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the optional title shown with the mobile name.
    /// </summary>
    [MemoryPackOrder(3)]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the configured brain identifier used by scripted AI.
    /// </summary>
    [MemoryPackOrder(4)]
    public string? BrainId { get; set; }

    /// <summary>
    /// Gets or sets the mobile world location.
    /// </summary>
    [MemoryPackOrder(5)]
    public Point3D Location { get; set; }

    /// <summary>
    /// Gets or sets the world map identifier.
    /// </summary>
    [MemoryPackOrder(6)]
    public int MapId { get; set; }

    /// <summary>
    /// Gets or sets the resolved world map for this mobile.
    /// </summary>
    [MemoryPackIgnore]
    public UoMap? Map
    {
        get => UoMap.GetMap(MapId);
        set => MapId = value?.Index ?? 0;
    }

    /// <summary>
    /// Gets or sets the world-facing direction.
    /// </summary>
    [MemoryPackOrder(7)]
    public DirectionType Direction { get; set; }

    /// <summary>
    /// Gets or sets whether this mobile is player-controlled.
    /// </summary>
    [MemoryPackOrder(8)]
    public bool IsPlayer { get; set; }

    /// <summary>
    /// Gets or sets whether this mobile is alive.
    /// </summary>
    [MemoryPackOrder(9)]
    public bool IsAlive { get; set; } = true;

    /// <summary>
    /// Gets or sets the mobile gender.
    /// </summary>
    [MemoryPackOrder(10)]
    public GenderType Gender { get; set; }

    /// <summary>
    /// Gets or sets the race table index.
    /// </summary>
    [MemoryPackOrder(11)]
    public byte RaceIndex { get; set; }

    /// <summary>
    /// Gets or sets the resolved race descriptor.
    /// </summary>
    [MemoryPackIgnore]
    public Race? Race
    {
        get => RaceIndex < Race.Races.Length ? Race.Races[RaceIndex] : null;
        set => RaceIndex = value is null ? (byte)0 : (byte)value.RaceIndex;
    }

    /// <summary>
    /// Gets or sets the profession identifier.
    /// </summary>
    [MemoryPackOrder(12)]
    public int ProfessionId { get; set; }

    /// <summary>
    /// Gets or sets the resolved profession descriptor.
    /// </summary>
    [MemoryPackIgnore]
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
    [MemoryPackOrder(13)]
    public short SkinHue { get; set; }

    /// <summary>
    /// Gets or sets the hair style.
    /// </summary>
    [MemoryPackOrder(14)]
    public short HairStyle { get; set; }

    /// <summary>
    /// Gets or sets the hair hue.
    /// </summary>
    [MemoryPackOrder(15)]
    public short HairHue { get; set; }

    /// <summary>
    /// Gets or sets the facial hair style.
    /// </summary>
    [MemoryPackOrder(16)]
    public short FacialHairStyle { get; set; }

    /// <summary>
    /// Gets or sets the facial hair hue.
    /// </summary>
    [MemoryPackOrder(17)]
    public short FacialHairHue { get; set; }

    /// <summary>
    /// Gets or sets the explicit base body override.
    /// </summary>
    [MemoryPackOrder(18)]
    public Body? BaseBody { get; set; }

    /// <summary>
    /// Gets or sets the resolved body value exposed to packets and gameplay systems.
    /// </summary>
    [MemoryPackIgnore]
    public Body Body
    {
        get => GetBody();
        set => SetBody(value);
    }

    /// <summary>
    /// Gets or sets the persisted base stat values.
    /// </summary>
    [MemoryPackOrder(19)]
    public MobileStats BaseStats { get; set; } = new();

    /// <summary>
    /// Gets or sets the persisted base resistance values.
    /// </summary>
    [MemoryPackOrder(20)]
    public MobileResistances BaseResistances { get; set; } = new();

    /// <summary>
    /// Gets or sets the current and maximum resource values.
    /// </summary>
    [MemoryPackOrder(21)]
    public MobileResources Resources { get; set; } = new();

    /// <summary>
    /// Gets or sets the aggregated modifiers coming from equipped items.
    /// </summary>
    [MemoryPackOrder(22)]
    public MobileModifiers? EquipmentModifiers { get; set; }

    /// <summary>
    /// Gets or sets the aggregated runtime modifiers coming from buffs and debuffs.
    /// </summary>
    [MemoryPackOrder(23)]
    public MobileModifiers? RuntimeModifiers { get; set; }

    /// <summary>
    /// Gets or sets the modifier cap values used by modern status packets and effect validation.
    /// </summary>
    [MemoryPackOrder(24)]
    public MobileModifierCaps ModifierCaps { get; set; } = new();

    /// <summary>
    /// Gets or sets the base strength value.
    /// </summary>
    [MemoryPackIgnore]
    public int Strength
    {
        get => BaseStats.Strength;
        set => BaseStats.Strength = value;
    }

    /// <summary>
    /// Gets or sets the base dexterity value.
    /// </summary>
    [MemoryPackIgnore]
    public int Dexterity
    {
        get => BaseStats.Dexterity;
        set => BaseStats.Dexterity = value;
    }

    /// <summary>
    /// Gets or sets the base intelligence value.
    /// </summary>
    [MemoryPackIgnore]
    public int Intelligence
    {
        get => BaseStats.Intelligence;
        set => BaseStats.Intelligence = value;
    }

    /// <summary>
    /// Gets or sets the current hit points.
    /// </summary>
    [MemoryPackIgnore]
    public int Hits
    {
        get => Resources.Hits;
        set => Resources.Hits = value;
    }

    /// <summary>
    /// Gets or sets the current mana value.
    /// </summary>
    [MemoryPackIgnore]
    public int Mana
    {
        get => Resources.Mana;
        set => Resources.Mana = value;
    }

    /// <summary>
    /// Gets or sets the current stamina value.
    /// </summary>
    [MemoryPackIgnore]
    public int Stamina
    {
        get => Resources.Stamina;
        set => Resources.Stamina = value;
    }

    /// <summary>
    /// Gets or sets the maximum hit points.
    /// </summary>
    [MemoryPackIgnore]
    public int MaxHits
    {
        get => Resources.MaxHits;
        set => Resources.MaxHits = value;
    }

    /// <summary>
    /// Gets or sets the maximum mana value.
    /// </summary>
    [MemoryPackIgnore]
    public int MaxMana
    {
        get => Resources.MaxMana;
        set => Resources.MaxMana = value;
    }

    /// <summary>
    /// Gets or sets the maximum stamina value.
    /// </summary>
    [MemoryPackIgnore]
    public int MaxStamina
    {
        get => Resources.MaxStamina;
        set => Resources.MaxStamina = value;
    }

    /// <summary>
    /// Gets or sets the remaining unallocated skill points, when used by creation or progression flows.
    /// </summary>
    [MemoryPackOrder(25)]
    public int SkillPoints { get; set; }

    /// <summary>
    /// Gets or sets the remaining unallocated stat points, when used by creation or progression flows.
    /// </summary>
    [MemoryPackOrder(26)]
    public int StatPoints { get; set; }

    /// <summary>
    /// Gets or sets the total stat cap.
    /// </summary>
    [MemoryPackOrder(27)]
    public int StatCap { get; set; } = 225;

    /// <summary>
    /// Gets or sets the current follower slot usage.
    /// </summary>
    [MemoryPackOrder(28)]
    public int Followers { get; set; }

    /// <summary>
    /// Gets or sets the maximum follower slots.
    /// </summary>
    [MemoryPackOrder(29)]
    public int FollowersMax { get; set; } = 5;

    /// <summary>
    /// Gets or sets the carried weight used by the modern status packet.
    /// </summary>
    [MemoryPackOrder(30)]
    public int Weight { get; set; }

    /// <summary>
    /// Gets or sets the current combat target.
    /// </summary>
    [MemoryPackIgnore]
    public Serial CombatantId { get; set; }

    /// <summary>
    /// Gets or sets the scheduled next combat resolution time in UTC.
    /// </summary>
    [MemoryPackIgnore]
    public DateTime? NextCombatAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the last time this mobile participated in combat in UTC.
    /// </summary>
    [MemoryPackIgnore]
    public DateTime? LastCombatAtUtc { get; set; }

    /// <summary>
    /// Gets or sets recent incoming aggression records for this mobile.
    /// </summary>
    [MemoryPackIgnore]
    public List<AggressorInfo> Aggressors { get; set; } = [];

    /// <summary>
    /// Gets or sets recent outgoing aggression records for this mobile.
    /// </summary>
    [MemoryPackIgnore]
    public List<AggressorInfo> Aggressed { get; set; } = [];

    /// <summary>
    /// Gets or sets the carrying capacity used by the modern status packet.
    /// </summary>
    [MemoryPackOrder(31)]
    public int MaxWeight { get; set; }

    /// <summary>
    /// Gets or sets the minimum weapon damage shown in the status packet.
    /// </summary>
    [MemoryPackOrder(32)]
    public int MinWeaponDamage { get; set; }

    /// <summary>
    /// Gets or sets the maximum weapon damage shown in the status packet.
    /// </summary>
    [MemoryPackOrder(33)]
    public int MaxWeaponDamage { get; set; }

    /// <summary>
    /// Gets or sets the tithing points shown in the status packet.
    /// </summary>
    [MemoryPackOrder(34)]
    public int Tithing { get; set; }

    /// <summary>
    /// Gets or sets the persisted skill table keyed by UO skill id.
    /// </summary>
    [MemoryPackOrder(53)]
    public Dictionary<UOSkillName, SkillEntry> Skills { get; set; } = [];

    /// <summary>
    /// Gets or sets runtime sound slots copied from the mobile template.
    /// </summary>
    [MemoryPackOrder(54)]
    public Dictionary<MobileSoundType, int> Sounds { get; set; } = [];

    /// <summary>
    /// Gets or sets the base fire resistance.
    /// </summary>
    [MemoryPackIgnore]
    public int FireResistance
    {
        get => BaseResistances.Fire;
        set => BaseResistances.Fire = value;
    }

    /// <summary>
    /// Gets or sets the base cold resistance.
    /// </summary>
    [MemoryPackIgnore]
    public int ColdResistance
    {
        get => BaseResistances.Cold;
        set => BaseResistances.Cold = value;
    }

    /// <summary>
    /// Gets or sets the base poison resistance.
    /// </summary>
    [MemoryPackIgnore]
    public int PoisonResistance
    {
        get => BaseResistances.Poison;
        set => BaseResistances.Poison = value;
    }

    /// <summary>
    /// Gets or sets the base energy resistance.
    /// </summary>
    [MemoryPackIgnore]
    public int EnergyResistance
    {
        get => BaseResistances.Energy;
        set => BaseResistances.Energy = value;
    }

    /// <summary>
    /// Gets or sets the base luck value before modifiers are applied.
    /// </summary>
    [MemoryPackOrder(35)]
    public int BaseLuck { get; set; }

    /// <summary>
    /// Gets or sets the legacy luck alias backed by <see cref="BaseLuck" />.
    /// </summary>
    [MemoryPackIgnore]
    public int Luck
    {
        get => BaseLuck;
        set => BaseLuck = value;
    }

    /// <summary>
    /// Gets the effective strength after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveStrength => Strength + GetModifierValue(static modifier => modifier.StrengthBonus);

    /// <summary>
    /// Gets the effective dexterity after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveDexterity => Dexterity + GetModifierValue(static modifier => modifier.DexterityBonus);

    /// <summary>
    /// Gets the effective intelligence after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveIntelligence => Intelligence + GetModifierValue(static modifier => modifier.IntelligenceBonus);

    /// <summary>
    /// Gets the effective physical resistance after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectivePhysicalResistance
        => BaseResistances.Physical + GetModifierValue(static modifier => modifier.PhysicalResist);

    /// <summary>
    /// Gets the effective fire resistance after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveFireResistance => FireResistance + GetModifierValue(static modifier => modifier.FireResist);

    /// <summary>
    /// Gets the effective cold resistance after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveColdResistance => ColdResistance + GetModifierValue(static modifier => modifier.ColdResist);

    /// <summary>
    /// Gets the effective poison resistance after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectivePoisonResistance => PoisonResistance + GetModifierValue(static modifier => modifier.PoisonResist);

    /// <summary>
    /// Gets the effective energy resistance after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveEnergyResistance => EnergyResistance + GetModifierValue(static modifier => modifier.EnergyResist);

    /// <summary>
    /// Gets the effective luck after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveLuck => BaseLuck + GetModifierValue(static modifier => modifier.Luck);

    /// <summary>
    /// Gets the effective hit chance increase after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveHitChanceIncrease => GetModifierValue(static modifier => modifier.HitChanceIncrease);

    /// <summary>
    /// Gets the effective defense chance increase after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveDefenseChanceIncrease => GetModifierValue(static modifier => modifier.DefenseChanceIncrease);

    /// <summary>
    /// Gets the effective damage increase after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveDamageIncrease => GetModifierValue(static modifier => modifier.DamageIncrease);

    /// <summary>
    /// Gets the effective swing speed increase after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveSwingSpeedIncrease => GetModifierValue(static modifier => modifier.SwingSpeedIncrease);

    /// <summary>
    /// Gets the effective spell damage increase after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveSpellDamageIncrease => GetModifierValue(static modifier => modifier.SpellDamageIncrease);

    /// <summary>
    /// Gets the effective faster casting value after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveFasterCasting => GetModifierValue(static modifier => modifier.FasterCasting);

    /// <summary>
    /// Gets the effective faster cast recovery value after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveFasterCastRecovery => GetModifierValue(static modifier => modifier.FasterCastRecovery);

    /// <summary>
    /// Gets the effective lower mana cost after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveLowerManaCost => GetModifierValue(static modifier => modifier.LowerManaCost);

    /// <summary>
    /// Gets the effective lower reagent cost after equipment and runtime modifiers.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveLowerReagentCost => GetModifierValue(static modifier => modifier.LowerReagentCost);

    /// <summary>
    /// Gets or sets the serial of the backpack item.
    /// </summary>
    [MemoryPackOrder(36)]
    public Serial BackpackId { get; set; }

    /// <summary>
    /// Gets equipped item references by layer.
    /// </summary>
    [MemoryPackOrder(37)]
    public Dictionary<ItemLayerType, Serial> EquippedItemIds { get; set; } = [];

    /// <summary>
    /// Gets runtime equipped-item snapshots keyed by equipment layer.
    /// This cache is not used for persistence.
    /// </summary>
    [MemoryPackIgnore]
    public IReadOnlyDictionary<ItemLayerType, ItemReference> EquippedItemReferences => _equippedItemReferences;

    /// <summary>
    /// Gets runtime total gold in backpack and bank box.
    /// </summary>
    [MemoryPackIgnore]
    public int Gold => GetGold();

    /// <summary>
    /// Gets the effective carried weight used by player status packets.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveCarriedWeight => DefaultBodyWeight + GetCarriedItemWeight();

    /// <summary>
    /// Gets the effective carrying capacity used by player status packets.
    /// </summary>
    [MemoryPackIgnore]
    public int EffectiveMaxWeight
    {
        get
        {
            if (!IsPlayer)
            {
                return MaxWeight;
            }

            var baseWeight = RaceIndex == 0 ? HumanMaxWeightBase : NonHumanMaxWeightBase;

            return baseWeight + (int)(3.5 * EffectiveStrength);
        }
    }

    /// <summary>
    /// Gets persisted custom mobile properties.
    /// </summary>
    [MemoryPackIgnore]
    public IReadOnlyDictionary<string, ItemCustomProperty> CustomProperties => _customProperties;

    /// <summary>
    /// Gets or sets whether the mobile is in war mode.
    /// </summary>
    [MemoryPackOrder(39)]
    public bool IsWarMode { get; set; }

    /// <summary>
    /// Gets or sets the legacy warmode alias backed by <see cref="IsWarMode" />.
    /// </summary>
    [MemoryPackIgnore]
    public bool Warmode
    {
        get => IsWarMode;
        set => IsWarMode = value;
    }

    /// <summary>
    /// Gets or sets the hunger level.
    /// </summary>
    [MemoryPackOrder(40)]
    public int Hunger { get; set; }

    /// <summary>
    /// Gets or sets the thirst level.
    /// </summary>
    [MemoryPackOrder(41)]
    public int Thirst { get; set; }

    /// <summary>
    /// Gets or sets the fame value.
    /// </summary>
    [MemoryPackOrder(42)]
    public int Fame { get; set; }

    /// <summary>
    /// Gets or sets the karma value.
    /// </summary>
    [MemoryPackOrder(43)]
    public int Karma { get; set; }

    /// <summary>
    /// Gets or sets the murder count.
    /// </summary>
    [MemoryPackOrder(44)]
    public int Kills { get; set; }

    /// <summary>
    /// Gets or sets the legacy hidden alias backed by <see cref="IsHidden" />.
    /// </summary>
    [MemoryPackIgnore]
    public bool Hidden
    {
        get => IsHidden;
        set => IsHidden = value;
    }

    /// <summary>
    /// Gets or sets whether the mobile is hidden.
    /// </summary>
    [MemoryPackOrder(45)]
    public bool IsHidden { get; set; }

    /// <summary>
    /// Gets or sets whether the mobile is frozen.
    /// </summary>
    [MemoryPackOrder(46)]
    public bool IsFrozen { get; set; }

    /// <summary>
    /// Gets or sets whether the mobile is paralyzed.
    /// </summary>
    [MemoryPackOrder(47)]
    public bool IsParalyzed { get; set; }

    /// <summary>
    /// Gets or sets whether the mobile is flying.
    /// </summary>
    [MemoryPackOrder(48)]
    public bool IsFlying { get; set; }

    /// <summary>
    /// Gets or sets whether the mobile ignores collision with other mobiles.
    /// </summary>
    [MemoryPackOrder(49)]
    public bool IgnoreMobiles { get; set; }

    /// <summary>
    /// Gets or sets whether the mobile is poisoned.
    /// </summary>
    [MemoryPackOrder(50)]
    public bool IsPoisoned { get; set; }

    /// <summary>
    /// Gets or sets the legacy blessed alias backed by <see cref="IsBlessed" />.
    /// </summary>
    [MemoryPackIgnore]
    public bool Blessed
    {
        get => IsBlessed;
        set => IsBlessed = value;
    }

    /// <summary>
    /// Gets or sets whether the mobile is blessed.
    /// </summary>
    [MemoryPackOrder(51)]
    public bool IsBlessed { get; set; }

    /// <summary>
    /// Gets or sets whether the mobile is invulnerable.
    /// </summary>
    [MemoryPackOrder(52)]
    public bool IsInvulnerable { get; set; }

    /// <summary>
    /// Gets or sets the mounted companion mobile identifier for this rider.
    /// </summary>
    [MemoryPackOrder(55)]
    public Serial MountedMobileId { get; set; }

    /// <summary>
    /// Gets or sets the rider mobile identifier for this mount.
    /// </summary>
    [MemoryPackOrder(56)]
    public Serial RiderMobileId { get; set; }

    /// <summary>
    /// Gets or sets the visual item identifier projected on the mount layer while this mobile is mounted.
    /// </summary>
    [MemoryPackOrder(57)]
    public int MountedDisplayItemId { get; set; }

    /// <summary>
    /// Gets or sets whether this mobile can be mounted according to loaded mount tile data.
    /// </summary>
    [MemoryPackIgnore]
    public bool IsMountable { get; set; }

    /// <summary>
    /// Gets or sets whether the mobile is mounted.
    /// </summary>
    [MemoryPackIgnore]
    public bool IsMounted
    {
        get => MountedMobileId != Serial.Zero;
        set
        {
            if (!value)
            {
                MountedMobileId = Serial.Zero;
                MountedDisplayItemId = 0;
            }
            else if (MountedMobileId == Serial.Zero)
            {
                MountedMobileId = Id;
            }
        }
    }

    /// <summary>
    /// Gets or sets the notoriety level.
    /// </summary>
    [MemoryPackOrder(58)]
    public Notoriety Notoriety { get; set; } = Notoriety.Innocent;

    /// <summary>
    /// Gets or sets the creation timestamp in UTC.
    /// </summary>
    [MemoryPackOrder(59)]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the last login timestamp in UTC.
    /// </summary>
    [MemoryPackOrder(60)]
    public DateTime LastLoginUtc { get; set; } = DateTime.UtcNow;

    [MemoryPackOnDeserialized]
    private void OnMemoryPackDeserialized()
    {
        _customProperties = _customProperties.Count == 0
            ? new(StringComparer.Ordinal)
            : new(_customProperties, StringComparer.Ordinal);

        foreach (var skill in Skills)
        {
            skill.Value.Skill = ResolveSkillInfo(skill.Key);
        }
    }

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
        RecalculateDisplayedWeaponDamage();
    }

    /// <summary>
    /// Associates an equipped item id with this mobile without item metadata updates.
    /// </summary>
    public void AddEquippedItem(ItemLayerType layer, Serial itemId)
    {
        EquippedItemIds[layer] = itemId;
        _equippedItemReferences.Remove(layer);
        _equippedItemsRuntime.Remove(layer);
        RecalculateDisplayedWeaponDamage();
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
    /// Clears the current combat state for this mobile.
    /// </summary>
    public void ClearCombatState()
    {
        CombatantId = Serial.Zero;
        NextCombatAtUtc = null;
    }

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

        RecalculateDisplayedWeaponDamage();
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
    /// Tries to build the virtual mount-layer item reference used for client sync.
    /// </summary>
    public bool TryGetMountDisplayItemReference(out ItemReference itemReference)
    {
        if (MountedDisplayItemId <= 0)
        {
            itemReference = default;

            return false;
        }

        itemReference = new(
            (Serial)(Serial.ItemOffset | (Id.Value & MountVirtualSerialMask)),
            MountedDisplayItemId,
            0
        );

        return true;
    }

    /// <summary>
    /// Tries to get a configured sound id for the requested mobile sound slot.
    /// </summary>
    public bool TryGetSound(MobileSoundType type, out int soundId)
        => Sounds.TryGetValue(type, out soundId);

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
    /// Refreshes or appends a recent aggression relationship.
    /// </summary>
    public void RefreshAggressor(
        Serial attackerId,
        Serial defenderId,
        DateTime nowUtc,
        bool isCriminal = false,
        bool canReportMurder = false
    )
    {
        RefreshAggressorList(Aggressors, attackerId, defenderId, nowUtc, isCriminal, canReportMurder);
        RefreshAggressorList(Aggressed, attackerId, defenderId, nowUtc, isCriminal, canReportMurder);
    }

    /// <summary>
    /// Removes expired aggression entries from both aggression collections.
    /// </summary>
    public void ExpireAggressors(DateTime nowUtc, TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            return;
        }

        ExpireAggressorList(Aggressors, nowUtc, timeout);
        ExpireAggressorList(Aggressed, nowUtc, timeout);
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

        RecalculateDisplayedWeaponDamage();

        return removed;
    }

    private void RecalculateDisplayedWeaponDamage()
    {
        if (TryGetEquippedWeaponWithCombatStats(out var weapon))
        {
            MinWeaponDamage = Math.Max(0, weapon.CombatStats?.DamageMin ?? DefaultUnarmedMinWeaponDamage);
            MaxWeaponDamage = Math.Max(MinWeaponDamage, weapon.CombatStats?.DamageMax ?? DefaultUnarmedMaxWeaponDamage);

            return;
        }

        MinWeaponDamage = DefaultUnarmedMinWeaponDamage;
        MaxWeaponDamage = DefaultUnarmedMaxWeaponDamage;
    }

    private bool TryGetEquippedWeaponWithCombatStats(out UOItemEntity weapon)
    {
        if (TryGetEquippedWeaponWithCombatStats(ItemLayerType.OneHanded, out weapon))
        {
            return true;
        }

        if (TryGetEquippedWeaponWithCombatStats(ItemLayerType.TwoHanded, out weapon))
        {
            return true;
        }

        weapon = null!;

        return false;
    }

    private bool TryGetEquippedWeaponWithCombatStats(ItemLayerType layer, out UOItemEntity weapon)
    {
        if (_equippedItemsRuntime.TryGetValue(layer, out var equippedItem) &&
            equippedItem.CombatStats is { DamageMin: > 0, DamageMax: > 0 })
        {
            weapon = equippedItem;

            return true;
        }

        weapon = null!;

        return false;
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

    private int GetCarriedItemWeight()
    {
        var visited = new HashSet<Serial>();
        long total = 0;

        foreach (var equippedItem in _equippedItemsRuntime.Values)
        {
            total += SumItemWeightRecursive(equippedItem, visited);
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

    private static void ExpireAggressorList(List<AggressorInfo> entries, DateTime nowUtc, TimeSpan timeout)
        => entries.RemoveAll(entry => nowUtc - entry.LastCombatAtUtc >= timeout);

    private static void RefreshAggressorList(
        List<AggressorInfo> entries,
        Serial attackerId,
        Serial defenderId,
        DateTime nowUtc,
        bool isCriminal,
        bool canReportMurder
    )
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].AttackerId == attackerId && entries[i].DefenderId == defenderId)
            {
                entries[i] = new(attackerId, defenderId, nowUtc, isCriminal, canReportMurder);

                return;
            }
        }

        entries.Add(new(attackerId, defenderId, nowUtc, isCriminal, canReportMurder));
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

    private static long SumItemWeightRecursive(UOItemEntity item, HashSet<Serial> visited)
    {
        if (!visited.Add(item.Id))
        {
            return 0;
        }

        return item.TotalWeight;
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
