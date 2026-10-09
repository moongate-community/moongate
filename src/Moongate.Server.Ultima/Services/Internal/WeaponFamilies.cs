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

    // The sounds of an arrow or a bolt that hits and that misses, as ModernUO's ranged weapons.
    public const int ShotHitSound = 0x234;
    public const int ShotMissSound = 0x238;

    /// <summary>
    ///     Gets the sound of a blow that hits.
    /// </summary>
    public static int HitSound(WeaponType? type)
    {
        return type switch
        {
            WeaponType.Sword or WeaponType.Fencing => 0x23B,
            WeaponType.Axe                         => 0x232,
            WeaponType.PoleArm                     => 0x237,
            WeaponType.Mace                        => 0x233,
            WeaponType.Bow or WeaponType.Crossbow  => ShotHitSound,
            _                                      => FistsHitSound
        };
    }

    /// <summary>
    ///     Gets the sound of a swing that misses.
    /// </summary>
    public static int MissSound(WeaponType? type)
    {
        return type switch
        {
            WeaponType.Sword or WeaponType.Axe       => 0x23A,
            WeaponType.PoleArm or WeaponType.Fencing => 0x238,
            WeaponType.Mace                          => 0x239,
            WeaponType.Bow or WeaponType.Crossbow    => ShotMissSound,
            _                                        => FistsMissSound
        };
    }

    /// <summary>
    ///     Gets the action of a human body on a mount that swings the weapon: the bows and the crossbows have their
    ///     own, a weapon of two hands another, and any other the one-hand swing.
    /// </summary>
    public static HumanAnimationType MountedAction(WeaponType? type, bool twoHanded)
    {
        return type switch
        {
            WeaponType.Bow      => HumanAnimationType.MountedAttackBow,
            WeaponType.Crossbow => HumanAnimationType.MountedAttackCrossbow,
            _                   => twoHanded || type == WeaponType.PoleArm
                ? HumanAnimationType.MountedAttack2H
                : HumanAnimationType.MountedAttack1H
        };
    }

    /// <summary>
    ///     Gets the action of a human body that swings the weapon.
    /// </summary>
    public static HumanAnimationType Action(WeaponType? type, bool twoHanded)
    {
        return type switch
        {
            WeaponType.Sword or WeaponType.Axe => twoHanded
                ? HumanAnimationType.AttackSlash2H
                : HumanAnimationType.AttackSlash1H,
            WeaponType.PoleArm  => HumanAnimationType.AttackSlash2H,
            WeaponType.Mace     => twoHanded ? HumanAnimationType.AttackBash2H : HumanAnimationType.AttackBash1H,
            WeaponType.Fencing  => twoHanded ? HumanAnimationType.AttackPierce2H : HumanAnimationType.AttackPierce1H,
            WeaponType.Bow      => HumanAnimationType.AttackBow,
            WeaponType.Crossbow => HumanAnimationType.AttackCrossbow,
            _                   => HumanAnimationType.Punch
        };
    }
}
