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
            WeaponType.Mace    => SkillType.MaceFighting,
            WeaponType.Fencing => SkillType.Fencing,
            WeaponType.Bow or WeaponType.Crossbow => SkillType.Archery,
            WeaponType.Thrown  => SkillType.Throwing,
            _                  => SkillType.Swordsmanship
        };

        /// <summary>
        ///     Gets whether the weapon shoots or throws: it is not fought in melee, which is all there is yet.
        /// </summary>
        public bool IsRanged => type is WeaponType.Bow or WeaponType.Crossbow or WeaponType.Thrown;
    }
}
