namespace Moongate.Server.Ultima.Types.Items;

/// <summary>
///     The kind of a weapon, as UOX3 types it by graphic: it decides the skill the weapon is fought with and, with
///     whether it is held in both hands, the swing animation and the sounds of a blow.
/// </summary>
public enum WeaponType
{
    /// <summary>
    ///     A sword, a cutlass, a katana: slashes, fought with swordsmanship.
    /// </summary>
    Sword,

    /// <summary>
    ///     An axe: slashes, fought with swordsmanship.
    /// </summary>
    Axe,

    /// <summary>
    ///     A halberd, a bardiche, a scythe: slashes, fought with swordsmanship.
    /// </summary>
    PoleArm,

    /// <summary>
    ///     A mace, a club, a hammer, a staff: bashes, fought with mace fighting.
    /// </summary>
    Mace,

    /// <summary>
    ///     A dagger, a spear, a kryss: pierces, fought with fencing.
    /// </summary>
    Fencing,

    /// <summary>
    ///     A bow or a blowgun: shot, fought with archery.
    /// </summary>
    Bow,

    /// <summary>
    ///     A crossbow: shot, fought with archery.
    /// </summary>
    Crossbow,

    /// <summary>
    ///     A boomerang or a cyclone: thrown, fought with throwing.
    /// </summary>
    Thrown
}
