using Moongate.Server.Core.Interfaces.Sessions;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Casts the spells of Magery by the classic rules: the checks, the words and the gesture, the delay of the circle in
///     which the caster cannot move, the target cursor, then the reagents, the mana and the skill check, a fizzle or the
///     script of the spell, and the recovery before the next cast. A cast is disturbed by damage, which ruins a spell
///     above the first circle while it is cast.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface ISpellCastService : ISessionClosedListener
{
    /// <summary>
    ///     "You do not have that spell!"
    /// </summary>
    const int MissingSpellMessage = 500015;

    /// <summary>
    ///     "You are frozen and can not move."
    /// </summary>
    const int FrozenMessage = 500111;

    /// <summary>
    ///     "This spell has been temporarily disabled."
    /// </summary>
    const int DisabledMessage = 502345;

    /// <summary>
    ///     "You are already casting a spell."
    /// </summary>
    const int AlreadyCastingMessage = 502642;

    /// <summary>
    ///     "You can not cast a spell while frozen."
    /// </summary>
    const int WhileFrozenMessage = 502643;

    /// <summary>
    ///     "You have not yet recovered from casting a spell."
    /// </summary>
    const int NotRecoveredMessage = 502644;

    /// <summary>
    ///     "Insufficient mana."
    /// </summary>
    const int NoManaMessage = 502625;

    /// <summary>
    ///     "More reagents are needed for this spell."
    /// </summary>
    const int NoReagentsMessage = 502630;

    /// <summary>
    ///     "The spell fizzles."
    /// </summary>
    const int FizzleMessage = 502632;

    /// <summary>
    ///     "Your concentration is disturbed, thus ruining thy spell."
    /// </summary>
    const int DisturbedMessage = 500641;

    /// <summary>
    ///     "That must be in your pack for you to use it."
    /// </summary>
    const int MustBeInPackMessage = 1042001;

    /// <summary>
    ///     "I am dead and cannot do that."
    /// </summary>
    const int DeadMessage = 1019048;

    /// <summary>
    ///     "That is too far away."
    /// </summary>
    const int TooFarMessage = 500446;

    /// <summary>
    ///     "Target can not be seen."
    /// </summary>
    const int CannotSeeMessage = 500237;

    /// <summary>
    ///     "This spell won't work on that!"
    /// </summary>
    const int WontWorkMessage = 501857;

    /// <summary>
    ///     "You cannot perform negative acts on your target."
    /// </summary>
    const int CannotHarmMessage = 1001018;

    /// <summary>
    ///     The most tiles away a target may be.
    /// </summary>
    const int TargetRange = 12;

    /// <summary>
    ///     Casts a spell the caster has in a spellbook it carries: <paramref name="preferred" />, the book a request named,
    ///     when it holds the spell, else the first book carried that does. The caster is told it has no such spell when none
    ///     does. False for a cast that did not begin.
    /// </summary>
    bool CastFromBook(MobileEntity caster, int spellId, ItemEntity? preferred = null);

    /// <summary>
    ///     Casts the spell of a scroll the caster carries in its backpack; one scroll is used up when the cast succeeds. False
    ///     for a cast that did not begin.
    /// </summary>
    bool CastFromScroll(MobileEntity caster, ItemEntity scroll);

    /// <summary>
    ///     Gets whether the caster is casting, its delay running or its target cursor waiting.
    /// </summary>
    bool IsCasting(MobileEntity caster);

    /// <summary>
    ///     Gets whether the caster may not move: its delay is running.
    /// </summary>
    bool BlocksMovement(MobileEntity caster);

    /// <summary>
    ///     Tells the service the caster took damage: a player's cast of a spell above the first circle, while its delay runs,
    ///     is ruined, and the next cast waits the less of the delay was done.
    /// </summary>
    void Hurt(MobileEntity caster);

    /// <summary>
    ///     Ends the cast of the caster, if any, with no message and no recovery, as its death does: the target cursor is
    ///     taken away.
    /// </summary>
    void Cancel(MobileEntity caster);
}
