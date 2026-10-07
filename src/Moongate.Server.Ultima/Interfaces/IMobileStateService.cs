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
    ///     The longest name a mobile can be given: what the status bar shows.
    /// </summary>
    const int MaxNameLength = 30;

    /// <summary>
    ///     Changes the numbers given. A stat or a maximum goes from 0 to <see cref="MaxValue" />; hit points, mana and
    ///     stamina are brought between 0 and their maximum, also when only the maximum changes. The mobile's player
    ///     gets the bars that moved, or its whole status when a stat, the fame or the karma changed, and the players
    ///     around the new health bar when the hit points or their maximum changed.
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
    ///     Sets which way a skill may move: up, down or locked, as the player chooses in the skill window. The skill is
    ///     added when the mobile has none yet. Nothing is sent: the client already shows the lock it asked for.
    /// </summary>
    /// <returns>
    ///     False, with nothing changed, for a skill or a lock that does not exist.
    /// </returns>
    bool SetSkillLock(MobileEntity mobile, SkillType skill, SkillLockType skillLock);

    /// <summary>
    ///     Sets which way a stat may move: up to rise by use, down to be lowered for another stat, or locked, as the
    ///     player chooses in the status window. When one changed, its own player is sent the three locks, as ModernUO does.
    /// </summary>
    /// <returns>
    ///     False, with nothing changed, for a stat or a lock that does not exist.
    /// </returns>
    bool SetStatLock(MobileEntity mobile, StatType stat, StatLockType statLock);

    /// <summary>
    ///     Gives the mobile another name, trimmed; its player's status and the players around are told.
    /// </summary>
    /// <returns>
    ///     False for a blank name or one longer than <see cref="MaxNameLength" />.
    /// </returns>
    bool SetName(MobileEntity mobile, string name);

    /// <summary>
    ///     Gives the mobile another body or another skin hue, null keeping what it is; its player and the players
    ///     around see it at once. Its player's step sequence starts again, as after a teleport.
    /// </summary>
    /// <returns>
    ///     False, with nothing changed, for a body or a hue outside 0 to 65535.
    /// </returns>
    bool SetLooks(MobileEntity mobile, int? body, int? hue);

    /// <summary>
    ///     Hides or reveals the mobile: hidden, it leaves the screens of the players around, who get it back when it is
    ///     revealed; the staff sees it either way.
    /// </summary>
    void SetHidden(MobileEntity mobile, bool hidden);

    /// <summary>
    ///     Freezes or frees the mobile: frozen, it neither steps nor turns.
    /// </summary>
    void SetFrozen(MobileEntity mobile, bool frozen);

    /// <summary>
    ///     Puts the mobile in war or peace mode; its player's client is told, also when nothing changed, as it waits
    ///     for the answer to its own request.
    /// </summary>
    void SetWarMode(MobileEntity mobile, bool warMode);

    /// <summary>
    ///     Makes the player a ghost or brings it back to its living body. A ghost wears the ghost body of its race and
    ///     gender and is hidden from the living players unless it is in war mode; its own client is told it died. The
    ///     living body is the one of the ghost body. Nothing happens for an NPC, or for a body that has no ghost.
    /// </summary>
    void SetDead(MobileEntity mobile, bool dead);

    /// <summary>
    ///     Sends the status of <paramref name="target" /> to the session: all of it for the session's own character,
    ///     else the name and the health bar only. Nothing is sent of a hidden mobile to a player who does not see it.
    /// </summary>
    void SendStatus(GameSession session, MobileEntity target);

    /// <summary>
    ///     Sends the session the skills of its character, as the skill window asks them.
    /// </summary>
    void SendSkills(GameSession session, MobileEntity character);
}
