using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Mobiles;

/// <summary>
///     One skill of a mobile, stored in the mobile's JSONB skill list. Values are in tenths of a point, as the
///     client counts them: 500 is 50.0.
/// </summary>
public class MobileSkill
{
    public SkillType Skill { get; set; }

    /// <summary>
    ///     The skill value without bonuses, in tenths of a point.
    /// </summary>
    public int Base { get; set; }

    /// <summary>
    ///     The highest value the skill can reach, in tenths of a point; 1000 is 100.0.
    /// </summary>
    public int Cap { get; set; } = 1000;

    public SkillLockType Lock { get; set; } = SkillLockType.Up;
}
