using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Tries a mobile at a skill and lets the skill rise with use, by ModernUO's rules for the classic game.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface ISkillService
{
    /// <summary>
    ///     Tries the mobile at <paramref name="skill" />, a task that one with <paramref name="min" /> points can just
    ///     begin and one with <paramref name="max" /> never fails; the points are whole and tenths, as 50.5. Below the
    ///     minimum it fails and at the maximum it succeeds, and neither teaches anything. In between the chance grows
    ///     in a line from 0 to 1, and the try, passed or failed, may raise the skill of a player by a tenth of a point:
    ///     the more room under its cap and under the cap of all skills, and the harder the task, the more often; a
    ///     failure teaches less. A skill locked or at its cap does not rise. At the cap of all skills a skill the
    ///     player set to go down drops to make room.
    /// </summary>
    bool Check(MobileEntity mobile, SkillType skill, double min, double max);

    /// <summary>
    ///     Gets the points the mobile has in all its skills together, in tenths.
    /// </summary>
    int Total(MobileEntity mobile);
}
