using Moongate.Server.Core.Interfaces.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Poisons mobiles: a poison of a level (0 lesser to 4 lethal) takes hits on a timer until it wears off, is cured or
///     kills. Its level is saved with the mobile, and the ticks start again when the player comes back.
/// </summary>
public interface IPoisonService : ISessionClosedListener
{
    /// <summary>
    ///     Poisons the mobile at <paramref name="level" />: a stronger poison replaces a weaker one and starts its count
    ///     again; an equal or weaker one changes nothing.
    /// </summary>
    PoisonResultType Apply(MobileEntity mobile, int level);

    /// <summary>
    ///     Ends the mobile's poison; false when it was not poisoned.
    /// </summary>
    bool Cure(MobileEntity mobile);

    /// <summary>
    ///     Gets the level of the mobile's poison; null when it is not poisoned.
    /// </summary>
    int? LevelOf(MobileEntity mobile);

    /// <summary>
    ///     Starts the ticks of a saved poison again, as a poisoned player comes back; nothing when they already run.
    /// </summary>
    void Resume(MobileEntity mobile);
}
