using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Changes what a live mobile is, its numbers, its skills, its name and its looks, and tells the clients that
///     show them: the status bar and the skill window of its own player, the health bar and the figure the players
///     around see.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface IMobileStateService
{
    /// <summary>
    ///     The highest value of a stat, of a maximum and of hit points, mana and stamina: what the status packet holds.
    /// </summary>
    const int MaxValue = ushort.MaxValue;

    /// <summary>
    ///     Changes the numbers given. A stat or a maximum goes from 0 to <see cref="MaxValue" />; hit points, mana and
    ///     stamina are brought between 0 and their maximum, also when only the maximum changes. The mobile's player
    ///     gets its status again, and the players around the new health bar when the hit points or their maximum
    ///     changed.
    /// </summary>
    /// <returns>
    ///     False, with nothing changed, when a stat or a maximum is out of range.
    /// </returns>
    bool SetStats(MobileEntity mobile, MobileStatsChange change);

    /// <summary>
    ///     Gets a skill of the mobile; one it never had is 0 with the usual cap, going up.
    /// </summary>
    MobileSkill GetSkill(MobileEntity mobile, SkillType skill);

    /// <summary>
    ///     Gets every skill of the game for the mobile, in the order of <see cref="SkillType" />.
    /// </summary>
    IReadOnlyList<MobileSkill> GetSkills(MobileEntity mobile);

    /// <summary>
    ///     Sets a skill, in tenths of a point, and its cap when given; the value is brought between 0 and the cap. The
    ///     mobile's player sees it in the skill window.
    /// </summary>
    /// <returns>
    ///     False, with nothing changed, for a skill that does not exist or a cap outside 0 to <see cref="MaxValue" />.
    /// </returns>
    bool SetSkill(MobileEntity mobile, SkillType skill, int value, int? cap = null);

    /// <summary>
    ///     Gives the mobile another name; its player's status and the players around are told.
    /// </summary>
    /// <returns>
    ///     False for a blank name.
    /// </returns>
    bool SetName(MobileEntity mobile, string name);

    /// <summary>
    ///     Gives the mobile another body or another skin hue, null keeping what it is; its player and the players
    ///     around see it at once.
    /// </summary>
    /// <returns>
    ///     False, with nothing changed, for a body or a hue outside 0 to 65535.
    /// </returns>
    bool SetLooks(MobileEntity mobile, int? body, int? hue);

    /// <summary>
    ///     Sends the status of <paramref name="target" /> to the session: all of it for the session's own character,
    ///     else the name and the health bar only.
    /// </summary>
    void SendStatus(GameSession session, MobileEntity target);

    /// <summary>
    ///     Sends the session the skills of its character, as the skill window asks them.
    /// </summary>
    void SendSkills(GameSession session, MobileEntity character);
}
