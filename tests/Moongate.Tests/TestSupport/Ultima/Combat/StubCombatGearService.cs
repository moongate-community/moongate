using Moongate.Server.Ultima.Data.Combat;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Combat;

namespace Moongate.Tests.TestSupport.Ultima.Combat;

/// <summary>
///     Gives every player the weapon in <see cref="Weapon" /> and the armor of <see cref="Armor" />, by part; NPCs are not
///     asked.
/// </summary>
public sealed class StubCombatGearService : ICombatGearService
{
    public WeaponInfo? Weapon { get; set; }

    public Dictionary<ArmorZoneType, int> Armor { get; } = [];

    /// <summary>
    ///     The bow or crossbow every mobile holds, when set.
    /// </summary>
    public WeaponInfo? Ranged { get; set; }

    public WeaponInfo? WeaponOf(MobileEntity mobile)
    {
        return Weapon;
    }

    public WeaponInfo? RangedWeaponOf(MobileEntity mobile)
    {
        return Ranged;
    }

    /// <summary>
    ///     The ammunition every shooter carries, when set.
    /// </summary>
    public ItemEntity? Ammo { get; set; }

    public ItemEntity? AmmoOf(MobileEntity mobile, WeaponInfo weapon, Func<ItemEntity, bool>? accept = null)
    {
        return Ammo;
    }

    public int ArmorAt(MobileEntity mobile, ArmorZoneType zone)
    {
        return Armor.GetValueOrDefault(zone);
    }

    public int ArmorRatingOf(MobileEntity mobile)
    {
        return Armor.Values.Sum();
    }

    /// <summary>
    ///     The damage <see cref="WithGear" /> gives the status of a player; none leaves it as it is.
    /// </summary>
    public (int Min, int Max)? StatusDamage { get; set; }

    public MobileStatusInfo WithGear(MobileStatusInfo status, MobileEntity mobile)
    {
        return StatusDamage is { } damage && !mobile.IsNpc
            ? status with { DamageMin = damage.Min, DamageMax = damage.Max, PhysicalResistance = Armor.Values.Sum() }
            : status;
    }
}
