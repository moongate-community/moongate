using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The skill trainers, as ModernUO's: an NPC teaches the skills it has at 60.0 or more, up to a third of its value
///     (42.0 at most) and the cap of the skill, for 1 gold a tenth of a point, paid by dropping gold on it. Game loop only.
/// </summary>
public interface ITrainingService : ISessionClosedListener
{
    /// <summary>
    ///     Gets the skills <paramref name="trainer" /> would teach <paramref name="player" /> more of than the player has,
    ///     whatever the locks and the total cap say; empty for a player who is dead.
    /// </summary>
    IReadOnlyList<SkillType> Teachable(MobileEntity trainer, MobileEntity player);

    /// <summary>
    ///     Tells the price of learning <paramref name="skill" /> from <paramref name="trainer" /> and keeps the quote for
    ///     the player, or has the trainer say why it cannot teach.
    /// </summary>
    /// <returns>True when a price was quoted.</returns>
    bool Quote(GameSession session, MobileEntity trainer, SkillType skill);

    /// <summary>
    ///     Takes the gold dropped on <paramref name="trainer" /> against the quote of the player: the points are the gold in
    ///     tenths, at most what was quoted, the skill rises at once, skills locked down give way to the total cap, and the
    ///     gold above the price is left with the player.
    /// </summary>
    /// <returns>True when gold was taken.</returns>
    bool Pay(GameSession session, MobileEntity trainer, ItemEntity gold);
}
