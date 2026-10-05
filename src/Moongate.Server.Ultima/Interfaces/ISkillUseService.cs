using Moongate.Server.Core.Data.Sessions;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Lets a player use a skill from its skill window or a macro: the wait between two skills and the script of the
///     skill.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface ISkillUseService
{
    /// <summary>
    ///     Uses the skill for the session's character. Before its wait is over the player reads "You must wait a few
    ///     moments to use another skill." (cliloc 500118); a skill without a script answers "That skill cannot be used
    ///     directly." (500014). Otherwise the script's <c>on_use</c> runs, and the number it returns is the seconds to
    ///     wait before another skill, one when it returns none. False when nothing ran.
    /// </summary>
    bool Use(GameSession session, SkillType skill);
}
