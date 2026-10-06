using Moongate.Server.Ultima.Types.Items;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     What a <see cref="WeaponType" /> says of how it is fought.
/// </summary>
public static class WeaponTypeExtensions
{
    extension(WeaponType type)
    {
        /// <summary>
        ///     Gets the skill the weapon is fought with, as UOX3's <c>GetCombatSkill</c>.
        /// </summary>
        public SkillType Skill => type switch
        {
            WeaponType.Mace                       => SkillType.MaceFighting,
            WeaponType.Fencing                    => SkillType.Fencing,
            WeaponType.Bow or WeaponType.Crossbow => SkillType.Archery,
            WeaponType.Thrown                     => SkillType.Throwing,
            _                                     => SkillType.Swordsmanship
        };

        /// <summary>
        ///     Gets how far the weapon reaches, in cells: 10 for a bow, 8 for a crossbow, as ModernUO; 1 for every other.
        /// </summary>
        public int Range => type switch
        {
            WeaponType.Bow      => 10,
            WeaponType.Crossbow => 8,
            _                   => 1
        };

        /// <summary>
        ///     Gets the graphic of the projectile that flies from the shooter to its target: the arrow 0x0F42 of a bow,
        ///     the bolt 0x1BFE of a crossbow; 0 for any other weapon.
        /// </summary>
        public int Projectile => type switch
        {
            WeaponType.Bow      => 0x0F42,
            WeaponType.Crossbow => 0x1BFE,
            _                   => 0
        };

        /// <summary>
        ///     Gets the item graphic of the ammunition the weapon spends at each shot: the arrow 0x0F3F of a bow, the bolt
        ///     0x1BFB of a crossbow; 0 for any other weapon.
        /// </summary>
        public int Ammo => type switch
        {
            WeaponType.Bow      => 0x0F3F,
            WeaponType.Crossbow => 0x1BFB,
            _                   => 0
        };

        /// <summary>
        ///     Gets whether the weapon shoots or throws: it is not fought in melee, which is all there is yet.
        /// </summary>
        public bool IsRanged => type is WeaponType.Bow or WeaponType.Crossbow or WeaponType.Thrown;
    }
}
