using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Teleports a mobile, a player or an NPC, as ModernUO's <c>MoveToWorld</c> does, on its map or to another: the
///     mobile stands on the new spot at once, and the clients are told.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface ITeleportService
{
    /// <summary>
    ///     Puts the mobile on <paramref name="location" /> of <paramref name="map" />. A player's client gets the map
    ///     change first (0xBF 0x08) when the map is another one, then its new position (0x20), and starts its step
    ///     sequence again; the players around the old spot lose the mobile and those around the new one see it. False,
    ///     with nothing changed or sent, for a mobile that is not in the world, a map that is not loaded or a spot
    ///     outside it.
    /// </summary>
    /// <remarks>
    ///     The season, the light, the weather and the music of the new map follow by themselves: they listen to the
    ///     player's region, which a map change always changes.
    /// </remarks>
    bool Teleport(MobileEntity mobile, MapType map, Point3D location);
}
