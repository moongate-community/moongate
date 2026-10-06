using Moongate.Server.Ultima.Data.Combat;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Combat;

namespace Moongate.Tests.TestSupport.Ultima.Combat;

/// <summary>
///     Gives every player the weapon in <see cref="Weapon" /> and the armor of <see cref="Armor" />, by part; NPCs are not asked.
/// </summary>
public sealed class StubCombatGearService : ICombatGearService
{
    public WeaponInfo? Weapon { get; set; }

    public Dictionary<ArmorZoneType, int> Armor { get; } = [];

    public WeaponInfo? WeaponOf(MobileEntity mobile)
    {
        return Weapon;
    }

    public int ArmorAt(MobileEntity mobile, ArmorZoneType zone)
    {
        return Armor.GetValueOrDefault(zone);
    }

    public int ArmorRatingOf(MobileEntity mobile)
    {
        return Armor.Values.Sum();
    }

    public MobileStatusInfo WithGear(MobileStatusInfo status, MobileEntity mobile)
    {
        return status;
    }
}
