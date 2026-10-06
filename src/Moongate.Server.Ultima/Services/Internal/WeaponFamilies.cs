using Moongate.Server.Ultima.Types.Items;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     How a kind of weapon sounds and swings, as ModernUO's weapon classes: a sword slashes, a mace bashes, a spear
///     pierces. A weapon without a kind, and the fists, are the fists'.
/// </summary>
internal static class WeaponFamilies
{
    public const int FistsHitSound = 0x135;
    public const int FistsMissSound = 0x239;

    /// <summary>
    ///     Gets the sound of a blow that hits.
    /// </summary>
    public static int HitSound(WeaponType? type)
    {
        return type switch
        {
            WeaponType.Sword or WeaponType.Fencing => 0x23B,
            WeaponType.Axe                          => 0x232,
            WeaponType.PoleArm                      => 0x237,
            WeaponType.Mace                         => 0x233,
            _                                       => FistsHitSound
        };
    }

    /// <summary>
    ///     Gets the sound of a swing that misses.
    /// </summary>
    public static int MissSound(WeaponType? type)
    {
        return type switch
        {
            WeaponType.Sword or WeaponType.Axe => 0x23A,
            WeaponType.PoleArm or WeaponType.Fencing => 0x238,
            WeaponType.Mace                    => 0x239,
            _                                  => FistsMissSound
        };
    }

    /// <summary>
    ///     Gets the action of a human body that swings the weapon.
    /// </summary>
    public static HumanAnimationType Action(WeaponType? type, bool twoHanded)
    {
        return type switch
        {
            WeaponType.Sword or WeaponType.Axe => twoHanded ? HumanAnimationType.AttackSlash2H : HumanAnimationType.AttackSlash1H,
            WeaponType.PoleArm                 => HumanAnimationType.AttackSlash2H,
            WeaponType.Mace                    => twoHanded ? HumanAnimationType.AttackBash2H : HumanAnimationType.AttackBash1H,
            WeaponType.Fencing                 => twoHanded ? HumanAnimationType.AttackPierce2H : HumanAnimationType.AttackPierce1H,
            _                                  => HumanAnimationType.Punch
        };
    }
}
