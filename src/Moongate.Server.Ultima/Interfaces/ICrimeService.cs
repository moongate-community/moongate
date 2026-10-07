using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Who is a criminal: a mobile that did a criminal act is one for a while, with its name in grey for those who see
///     it, as ModernUO; each new act starts the time again. The time is saved with the mobile, so leaving the world
///     does not clear it. Called on the game loop.
/// </summary>
public interface ICrimeService
{
    /// <summary>
    ///     Gets whether the mobile is a criminal now.
    /// </summary>
    bool IsCriminal(MobileEntity mobile);

    /// <summary>
    ///     Makes the mobile a criminal for the configured time, or starts that time again; a player is told the
    ///     first time.
    /// </summary>
    void MakeCriminal(MobileEntity mobile);

    /// <summary>
    ///     Gives the flag back to a mobile that enters the world with saved time left, telling no one: the packets
    ///     that show it come after. A time already over is dropped.
    /// </summary>
    void Restore(MobileEntity mobile);

    /// <summary>
    ///     Makes the mobile innocent again at once.
    /// </summary>
    void Pardon(MobileEntity mobile);
}
