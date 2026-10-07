using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Combat;

/// <summary>
///     The weapon a mobile fights with, read from its template: what a swing of it does and how it looks and sounds.
/// </summary>
/// <param name="Skill">
///     The skill the weapon is fought with, whose points are the hit chance.
/// </param>
/// <param name="Type">
///     The kind of the weapon, which gives its sounds and swing; null for one UOX3 does not type.
/// </param>
/// <param name="TwoHanded">
///     Whether it takes both hands.
/// </param>
/// <param name="DamageMin">
///     The least damage of a hit before the bonuses.
/// </param>
/// <param name="DamageMax">
///     The most damage of a hit before the bonuses.
/// </param>
/// <param name="Speed">
///     The speed of the weapon: a swing takes 15000 / ((stamina + 100) * speed) seconds.
/// </param>
public sealed record WeaponInfo(SkillType Skill, WeaponType? Type, bool TwoHanded, int DamageMin, int DamageMax, int Speed)
{
    /// <summary>
    ///     Gets how far the weapon reaches, in cells: 1 for a weapon fought from beside the target.
    /// </summary>
    public int Range => Type?.Range ?? 1;
}
