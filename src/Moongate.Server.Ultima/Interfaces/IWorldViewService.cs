using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Tells the players in range what they see of each other: who appears (0x78), moves or turns (0x77) and leaves
///     their view (0x1D), recomputed from the old and new position of each step as ModernUO and POL do.
/// </summary>
/// <remarks>
///     Game loop only. Only mobiles registered through <see cref="Entered" /> receive packets.
/// </remarks>
public interface IWorldViewService
{
    /// <summary>
    ///     Registers the player's session, shows it everyone in range and shows it to the players in range.
    /// </summary>
    void Entered(MobileEntity mobile, long sessionId);

    /// <summary>
    ///     Tells the players in range that the mobile, now at its current location, moved from
    ///     <paramref name="oldLocation" /> or turned on the spot.
    /// </summary>
    void Moved(MobileEntity mobile, Point3D oldLocation, bool running);

    /// <summary>
    ///     Removes the mobile from the screens in range, registered or not, and forgets its session; call it while the
    ///     mobile is still in the sector grid.
    /// </summary>
    void Left(MobileEntity mobile);
}
