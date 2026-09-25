using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Professions;

/// <summary>
///     One starting skill of a profession in <c>professions.toml</c>.
/// </summary>
public class ProfessionSkillContent
{
    /// <summary>
    ///     The skill, by name (such as <c>"Tactics"</c>); it must be listed in <c>skills.toml</c>.
    /// </summary>
    public SkillType Skill { get; set; }

    /// <summary>
    ///     The starting value of the skill, in whole points.
    /// </summary>
    public int Value { get; set; }
}
