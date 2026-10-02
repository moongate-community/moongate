using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Teleports a mobile, a player or an NPC, as ModernUO's <c>MoveToWorld</c> does on the same map: the mobile stands on
///     the new spot at once, and the clients are told.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface ITeleportService
{
    /// <summary>
    ///     Puts the mobile on <paramref name="location" /> of its own map. A player's client gets its new position (0x20)
    ///     and starts its step sequence again; the players around the old spot lose the mobile and those around the new
    ///     one see it. False, with nothing changed or sent, for a mobile that is not in the world or a spot outside the
    ///     map.
    /// </summary>
    bool Teleport(MobileEntity mobile, Point3D location);
}
